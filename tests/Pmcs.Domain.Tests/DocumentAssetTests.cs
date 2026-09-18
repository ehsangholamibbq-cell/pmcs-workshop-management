using System.Text;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Documents;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Documents.Scanning;

namespace Pmcs.Domain.Tests;

public sealed class DocumentAssetTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProjectDocumentStartsPendingWithServerRetentionAndVersion()
    {
        var asset = Create();

        Assert.Equal(DocumentAssetStatus.PendingUpload, asset.Status);
        Assert.Equal(DocumentScanVerdict.Pending, asset.ScanVerdict);
        Assert.Equal(1, asset.VersionNumber);
        Assert.Equal(CreatedAt.AddHours(24), asset.UploadExpiresAt);
        Assert.Equal(CreatedAt.AddYears(3), asset.RetainUntil);
    }

    [Fact]
    public void OwnerScopeCannotCrossProjectAndTenantBoundaries()
    {
        Assert.Equal(
            "documents.project.required",
            Assert.Throws<DomainRuleException>(() => DocumentAsset.CreatePending(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                DocumentOwnerType.ProjectGeneral,
                Guid.NewGuid(),
                1,
                "report.pdf",
                "application/pdf",
                1_024,
                new string('a', 64),
                $"documents/{Guid.NewGuid():N}.pdf",
                DocumentClassification.Internal,
                DocumentRetentionPolicy.Standard,
                null,
                false,
                Guid.NewGuid(),
                CreatedAt)).Code);
        Assert.Equal(
            "documents.project.not_allowed",
            Assert.Throws<DomainRuleException>(() => Create(
                ownerType: DocumentOwnerType.MemberProfile,
                projectId: Guid.NewGuid())).Code);

        var profile = Create(ownerType: DocumentOwnerType.MemberProfile, projectId: null);
        Assert.Null(profile.ProjectId);
    }

    [Fact]
    public void FileTypeExtensionHashAndSizeAreFailClosed()
    {
        Assert.Equal(
            "documents.content_type.unsupported",
            Assert.Throws<DomainRuleException>(() => Create(contentType: "application/x-msdownload")).Code);
        Assert.Equal(
            "documents.file_extension.mismatch",
            Assert.Throws<DomainRuleException>(() => Create(fileName: "invoice.exe")).Code);
        Assert.Equal(
            "documents.sha256.invalid",
            Assert.Throws<DomainRuleException>(() => Create(sha256: "abc")).Code);
        Assert.Equal(
            "documents.size.invalid",
            Assert.Throws<DomainRuleException>(() => Create(sizeBytes: DocumentAsset.MaximumSizeBytes + 1)).Code);
    }

    [Fact]
    public void CleanUploadMustRemainQuarantinedUntilCriticalRelease()
    {
        var asset = Create();

        asset.MarkQuarantined("etag", "scanner", "clean", CreatedAt.AddMinutes(2));
        Assert.Equal(DocumentAssetStatus.Quarantined, asset.Status);
        Assert.Equal(2, asset.Revision);

        asset.Release(2, Guid.NewGuid(), CreatedAt.AddMinutes(3));
        Assert.Equal(DocumentAssetStatus.Released, asset.Status);
        Assert.Equal(3, asset.Revision);
        Assert.NotNull(asset.ReleasedAt);
    }

    [Fact]
    public void ReleaseRejectsStaleRevisionAndNonCleanAsset()
    {
        var pending = Create();
        Assert.Equal(
            "documents.revision.conflict",
            Assert.Throws<DomainRuleException>(() => pending.Release(2, Guid.NewGuid(), CreatedAt)).Code);

        pending.MarkRejected(
            DocumentScanVerdict.Infected,
            "scanner",
            "signature",
            CreatedAt.AddMinutes(1));
        Assert.Equal(
            "documents.release.not_clean",
            Assert.Throws<DomainRuleException>(() => pending.Release(2, Guid.NewGuid(), CreatedAt)).Code);
    }

    [Fact]
    public void LegalHoldAndRetentionPreventEarlyDeletion()
    {
        var asset = Create();
        asset.UpdateGovernance(
            1,
            DocumentClassification.Restricted,
            DocumentRetentionPolicy.Standard,
            null,
            true,
            Guid.NewGuid(),
            CreatedAt.AddDays(1));

        Assert.Equal(
            "documents.retention.legal_hold",
            Assert.Throws<DomainRuleException>(() => asset.MarkDeleted(2, CreatedAt.AddYears(20))).Code);

        var retained = Create();
        Assert.Equal(
            "documents.retention.active",
            Assert.Throws<DomainRuleException>(() => retained.MarkDeleted(1, CreatedAt.AddYears(1))).Code);
        retained.MarkDeleted(1, CreatedAt.AddYears(3));
        Assert.Equal(DocumentAssetStatus.Deleted, retained.Status);
    }

    [Fact]
    public void RetryIdentityIncludesCustomRetentionIntent()
    {
        var customDate = CreatedAt.AddYears(5);
        var asset = Create(retainUntil: customDate);

        Assert.True(asset.MatchesUploadIntent(
            asset.ProjectId,
            asset.OwnerType,
            asset.OwnerId,
            asset.OriginalFileName,
            asset.ContentType,
            asset.SizeBytes,
            asset.Sha256,
            asset.Classification,
            asset.RetentionPolicy,
            customDate,
            asset.LegalHold));
        Assert.False(asset.MatchesUploadIntent(
            asset.ProjectId,
            asset.OwnerType,
            asset.OwnerId,
            asset.OriginalFileName,
            asset.ContentType,
            asset.SizeBytes,
            asset.Sha256,
            asset.Classification,
            asset.RetentionPolicy,
            customDate.AddDays(1),
            asset.LegalHold));
    }

    [Fact]
    public async Task ScannerRejectsDisguisedAndTestMalwareContent()
    {
        var scanner = new DeterministicContentScanner();
        var disguised = await scanner.ScanAsync(
            "report.pdf",
            "application/pdf",
            Encoding.ASCII.GetBytes("MZ executable"));
        var infected = await scanner.ScanAsync(
            "sample.txt",
            "text/plain",
            Encoding.ASCII.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE"));

        Assert.Equal(DocumentScanVerdict.Failed, disguised.Verdict);
        Assert.Equal(DocumentScanVerdict.Infected, infected.Verdict);
    }

    [Fact]
    public void ContentPolicyAllowsSafeProductFormatsAndRejectsMasquerading()
    {
        Assert.True(DocumentContentPolicy.MatchesSignature(
            "application/pdf",
            Encoding.ASCII.GetBytes("%PDF-1.7\n")));
        Assert.True(DocumentContentPolicy.MatchesSignature(
            "text/csv",
            Encoding.UTF8.GetBytes("ردیف,مقدار\n1,2")));
        Assert.False(DocumentContentPolicy.MatchesSignature(
            "application/pdf",
            Encoding.ASCII.GetBytes("MZ executable")));
        Assert.False(DocumentContentPolicy.MatchesSignature(
            "text/plain",
            new byte[] { 0x41, 0x00, 0x42 }));
    }

    [Fact]
    public void DocumentsManifestDeclaresCriticalReleaseSeparately()
    {
        var descriptor = new DocumentsModule().Descriptor;

        Assert.Equal("documents.shared", descriptor.ModuleId);
        Assert.Contains(descriptor.Permissions, permission =>
            permission.Key == "documents.release_quarantine" &&
            permission.Scope == Pmcs.BuildingBlocks.Modules.PermissionScope.Tenant &&
            permission.RiskClass == Pmcs.BuildingBlocks.Modules.ManifestRiskClass.Critical);
        Assert.Contains(descriptor.Events, integrationEvent =>
            integrationEvent.Name == "documents.asset.released" && integrationEvent.Version == 1);
    }

    private static DocumentAsset Create(
        DocumentOwnerType ownerType = DocumentOwnerType.ProjectGeneral,
        Guid? projectId = default,
        string fileName = "report.pdf",
        string contentType = "application/pdf",
        long sizeBytes = 1_024,
        string? sha256 = null,
        DateTimeOffset? retainUntil = null)
    {
        var resolvedProjectId = projectId == default && DocumentAsset.RequiresProject(ownerType)
            ? Guid.NewGuid()
            : projectId;
        return DocumentAsset.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            resolvedProjectId,
            ownerType,
            Guid.NewGuid(),
            1,
            fileName,
            contentType,
            sizeBytes,
            sha256 ?? new string('a', 64),
            $"documents/{Guid.NewGuid():N}.pdf",
            DocumentClassification.Internal,
            DocumentRetentionPolicy.Standard,
            retainUntil,
            false,
            Guid.NewGuid(),
            CreatedAt);
    }
}
