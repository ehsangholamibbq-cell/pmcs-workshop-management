using System.Text;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Evidence.Domain;

namespace Pmcs.Domain.Tests;

public sealed class EvidenceFileTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PendingEvidenceAcceptsSupportedPhotoWithoutPlanningBaselines()
    {
        var evidence = Create();

        Assert.Equal(EvidenceFileStatus.PendingUpload, evidence.Status);
        Assert.Equal(CreatedAt.AddHours(24), evidence.UploadExpiresAt);
        Assert.Null(evidence.DailyFactId);
    }

    [Fact]
    public void EvidenceRejectsUnsupportedBinaryType()
    {
        var exception = Assert.Throws<DomainRuleException>(() => Create(contentType: "application/x-msdownload"));

        Assert.Equal("evidence.content_type.unsupported", exception.Code);
    }

    [Fact]
    public void EvidenceRequiresValidSha256AndBoundedSize()
    {
        Assert.Equal(
            "evidence.sha256.invalid",
            Assert.Throws<DomainRuleException>(() => Create(sha256: "abc")).Code);
        Assert.Equal(
            "evidence.size.invalid",
            Assert.Throws<DomainRuleException>(() => Create(sizeBytes: EvidenceFile.MaximumSizeBytes + 1)).Code);
    }

    [Fact]
    public void UploadCompletionIsIdempotentAndAdvancesRevisionOnce()
    {
        var evidence = Create();

        evidence.MarkUploaded("etag-1", CreatedAt.AddMinutes(5));
        evidence.MarkUploaded("etag-1", CreatedAt.AddMinutes(6));

        Assert.Equal(EvidenceFileStatus.Uploaded, evidence.Status);
        Assert.Equal(2, evidence.Revision);
        Assert.Equal(CreatedAt.AddMinutes(5), evidence.UploadedAt);
    }

    [Fact]
    public void SupportedEvidenceSignaturesMatchTheirDeclaredContentTypes()
    {
        byte[] jpeg = [0xff, 0xd8, 0xff, 0xe0];
        byte[] png = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
        byte[] heic = [0, 0, 0, 24, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63];
        byte[] heif = [0, 0, 0, 24, 0x66, 0x74, 0x79, 0x70, 0x6d, 0x69, 0x66, 0x31];

        Assert.True(EvidenceContentPolicy.MatchesSignature("image/jpeg", jpeg));
        Assert.True(EvidenceContentPolicy.MatchesSignature("image/png", png));
        Assert.True(EvidenceContentPolicy.MatchesSignature(
            "image/webp", Encoding.ASCII.GetBytes("RIFF0000WEBP")));
        Assert.True(EvidenceContentPolicy.MatchesSignature("image/heic", heic));
        Assert.True(EvidenceContentPolicy.MatchesSignature("image/heif", heif));
        Assert.True(EvidenceContentPolicy.MatchesSignature(
            "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7\n")));
    }

    [Fact]
    public void EvidenceSignaturePolicyRejectsDisguisedContent()
    {
        var executable = Encoding.ASCII.GetBytes("MZ disguised executable");

        Assert.False(EvidenceContentPolicy.MatchesSignature("application/pdf", executable));
        Assert.False(EvidenceContentPolicy.MatchesSignature("image/jpeg", executable));
        Assert.False(EvidenceContentPolicy.MatchesSignature("image/png", executable));
        Assert.False(EvidenceContentPolicy.MatchesSignature("image/webp", executable));
        Assert.False(EvidenceContentPolicy.MatchesSignature("image/heic", executable));
        Assert.False(EvidenceContentPolicy.MatchesSignature("image/heif", executable));
    }

    [Fact]
    public void EvidenceObjectExtensionsAreDerivedFromVerifiedContentType()
    {
        Assert.Equal(".jpg", EvidenceContentPolicy.CanonicalExtension("image/jpeg"));
        Assert.Equal(".png", EvidenceContentPolicy.CanonicalExtension("image/png"));
        Assert.Equal(".webp", EvidenceContentPolicy.CanonicalExtension("image/webp"));
        Assert.Equal(".heic", EvidenceContentPolicy.CanonicalExtension("image/heic"));
        Assert.Equal(".heif", EvidenceContentPolicy.CanonicalExtension("image/heif"));
        Assert.Equal(".pdf", EvidenceContentPolicy.CanonicalExtension("application/pdf"));
    }

    private static EvidenceFile Create(
        string contentType = "image/jpeg",
        long sizeBytes = 1_024,
        string? sha256 = null) =>
        EvidenceFile.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "site-photo.jpg",
            contentType,
            sizeBytes,
            sha256 ?? new string('a', 64),
            $"evidence/{Guid.NewGuid():N}.jpg",
            CreatedAt,
            Guid.NewGuid(),
            CreatedAt);
}
