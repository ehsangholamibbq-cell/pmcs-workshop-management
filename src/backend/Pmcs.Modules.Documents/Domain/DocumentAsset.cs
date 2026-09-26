using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Documents.Domain;

public sealed class DocumentAsset : AggregateRoot
{
    public const long MaximumSizeBytes = 25L * 1024L * 1024L;
    public const int SessionLifetimeHours = 24;

    private DocumentAsset()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public DocumentOwnerType OwnerType { get; private set; }

    public Guid OwnerId { get; private set; }

    public int VersionNumber { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public string ObjectKey { get; private set; } = string.Empty;

    public DocumentClassification Classification { get; private set; }

    public DocumentRetentionPolicy RetentionPolicy { get; private set; }

    public DateTimeOffset? RetainUntil { get; private set; }

    public bool LegalHold { get; private set; }

    public DocumentAssetStatus Status { get; private set; }

    public DocumentScanVerdict ScanVerdict { get; private set; }

    public string? ScanProvider { get; private set; }

    public string? ScanDetails { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UploadExpiresAt { get; private set; }

    public DateTimeOffset? UploadedAt { get; private set; }

    public string? StorageETag { get; private set; }

    public Guid? ReleasedBy { get; private set; }

    public DateTimeOffset? ReleasedAt { get; private set; }

    public Guid? ClassifiedBy { get; private set; }

    public DateTimeOffset? ClassifiedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static DocumentAsset CreatePending(
        Guid id,
        Guid tenantId,
        Guid? projectId,
        DocumentOwnerType ownerType,
        Guid ownerId,
        int versionNumber,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string objectKey,
        DocumentClassification classification,
        DocumentRetentionPolicy retentionPolicy,
        DateTimeOffset? retainUntil,
        bool legalHold,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || ownerId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "documents.identity.required",
                "Document, tenant, owner and creator ids are required.");
        }

        if (!Enum.IsDefined(ownerType) ||
            !Enum.IsDefined(classification) ||
            !Enum.IsDefined(retentionPolicy))
        {
            throw new DomainRuleException(
                "documents.metadata.invalid",
                "Owner type, classification and retention policy must be recognized values.");
        }

        if (RequiresProject(ownerType) && (!projectId.HasValue || projectId.Value == Guid.Empty))
        {
            throw new DomainRuleException(
                "documents.project.required",
                "A project-owned document must declare its project boundary.");
        }

        if (!RequiresProject(ownerType) && projectId.HasValue)
        {
            throw new DomainRuleException(
                "documents.project.not_allowed",
                "A tenant-owned document cannot declare a project boundary.");
        }

        if (versionNumber <= 0)
        {
            throw new DomainRuleException("documents.version.invalid", "Document version must be positive.");
        }

        if (sizeBytes <= 0 || sizeBytes > MaximumSizeBytes)
        {
            throw new DomainRuleException(
                "documents.size.invalid",
                $"Document must be between 1 and {MaximumSizeBytes} bytes.");
        }

        var normalizedContentType = Required(contentType, 160, "documents.content_type.invalid")
            .ToLowerInvariant();
        if (!DocumentContentPolicy.IsAllowedContentType(normalizedContentType))
        {
            throw new DomainRuleException(
                "documents.content_type.unsupported",
                "The declared content type is not allowed by the shared document policy.");
        }

        var normalizedFileName = Required(originalFileName, 255, "documents.file_name.invalid");
        if (!DocumentContentPolicy.IsFileNameCompatible(normalizedFileName, normalizedContentType))
        {
            throw new DomainRuleException(
                "documents.file_extension.mismatch",
                "The file extension does not match the declared content type.");
        }

        var normalizedHash = Required(sha256, 64, "documents.sha256.invalid").ToLowerInvariant();
        if (normalizedHash.Length != 64 || normalizedHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainRuleException(
                "documents.sha256.invalid",
                "SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        var normalizedRetainUntil = NormalizeRetention(retentionPolicy, retainUntil, createdAt);
        return new DocumentAsset
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            OwnerType = ownerType,
            OwnerId = ownerId,
            VersionNumber = versionNumber,
            OriginalFileName = normalizedFileName,
            ContentType = normalizedContentType,
            SizeBytes = sizeBytes,
            Sha256 = normalizedHash,
            ObjectKey = Required(objectKey, 700, "documents.object_key.invalid"),
            Classification = classification,
            RetentionPolicy = retentionPolicy,
            RetainUntil = normalizedRetainUntil,
            LegalHold = legalHold,
            Status = DocumentAssetStatus.PendingUpload,
            ScanVerdict = DocumentScanVerdict.Pending,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UploadExpiresAt = createdAt.AddHours(SessionLifetimeHours)
        };
    }

    public void RenewUploadSession(DateTimeOffset renewedAt)
    {
        if (Status != DocumentAssetStatus.PendingUpload)
        {
            throw new DomainRuleException(
                "documents.upload.invalid_state",
                "Only a pending document can receive a renewed upload session.");
        }

        UploadExpiresAt = renewedAt.AddHours(SessionLifetimeHours);
        AdvanceRevision();
    }

    public void MarkQuarantined(
        string storageETag,
        string scanProvider,
        string? scanDetails,
        DateTimeOffset uploadedAt)
    {
        if (Status == DocumentAssetStatus.Quarantined)
        {
            return;
        }

        if (Status != DocumentAssetStatus.PendingUpload)
        {
            throw new DomainRuleException(
                "documents.upload.invalid_state",
                "Only a pending document can enter quarantine.");
        }

        Status = DocumentAssetStatus.Quarantined;
        ScanVerdict = DocumentScanVerdict.Clean;
        ScanProvider = Required(scanProvider, 120, "documents.scan_provider.invalid");
        ScanDetails = Optional(scanDetails, 500, "documents.scan_details.invalid");
        StorageETag = Required(storageETag, 200, "documents.storage_etag.invalid");
        UploadedAt = uploadedAt;
        AdvanceRevision();
    }

    public void MarkRejected(
        DocumentScanVerdict verdict,
        string scanProvider,
        string? scanDetails,
        DateTimeOffset rejectedAt)
    {
        if (verdict is not DocumentScanVerdict.Infected and not DocumentScanVerdict.Failed)
        {
            throw new DomainRuleException(
                "documents.scan_verdict.invalid",
                "A rejected document requires an infected or failed scan verdict.");
        }

        if (Status != DocumentAssetStatus.PendingUpload)
        {
            throw new DomainRuleException(
                "documents.upload.invalid_state",
                "Only a pending document can be rejected by the scanner.");
        }

        Status = DocumentAssetStatus.Rejected;
        ScanVerdict = verdict;
        ScanProvider = Required(scanProvider, 120, "documents.scan_provider.invalid");
        ScanDetails = Optional(scanDetails, 500, "documents.scan_details.invalid");
        UploadedAt = rejectedAt;
        AdvanceRevision();
    }

    public void Release(long baseRevision, Guid releasedBy, DateTimeOffset releasedAt)
    {
        RequireRevision(baseRevision);
        if (releasedBy == Guid.Empty)
        {
            throw new DomainRuleException("documents.release.actor_required", "A release actor is required.");
        }

        if (Status != DocumentAssetStatus.Quarantined || ScanVerdict != DocumentScanVerdict.Clean)
        {
            throw new DomainRuleException(
                "documents.release.not_clean",
                "Only a clean quarantined document can be released.");
        }

        Status = DocumentAssetStatus.Released;
        ReleasedBy = releasedBy;
        ReleasedAt = releasedAt;
        AdvanceRevision();
    }

    public void UpdateGovernance(
        long baseRevision,
        DocumentClassification classification,
        DocumentRetentionPolicy retentionPolicy,
        DateTimeOffset? retainUntil,
        bool legalHold,
        Guid classifiedBy,
        DateTimeOffset classifiedAt)
    {
        RequireRevision(baseRevision);
        if (classifiedBy == Guid.Empty)
        {
            throw new DomainRuleException(
                "documents.classification.actor_required",
                "A classification actor is required.");
        }

        if (Status == DocumentAssetStatus.Deleted)
        {
            throw new DomainRuleException(
                "documents.classification.deleted",
                "A deleted document cannot be reclassified.");
        }

        Classification = classification;
        RetentionPolicy = retentionPolicy;
        RetainUntil = NormalizeRetention(retentionPolicy, retainUntil, classifiedAt);
        LegalHold = legalHold;
        ClassifiedBy = classifiedBy;
        ClassifiedAt = classifiedAt;
        AdvanceRevision();
    }

    public void MarkDeleted(long baseRevision, DateTimeOffset deletedAt)
    {
        RequireRevision(baseRevision);
        if (LegalHold)
        {
            throw new DomainRuleException(
                "documents.retention.legal_hold",
                "A document under legal hold cannot be deleted.");
        }

        if (RetentionPolicy == DocumentRetentionPolicy.Permanent || RetainUntil > deletedAt)
        {
            throw new DomainRuleException(
                "documents.retention.active",
                "The active retention policy prevents deletion.");
        }

        Status = DocumentAssetStatus.Deleted;
        DeletedAt = deletedAt;
        AdvanceRevision();
    }

    public bool MatchesUploadIntent(
        Guid? projectId,
        DocumentOwnerType ownerType,
        Guid ownerId,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        DocumentClassification classification,
        DocumentRetentionPolicy retentionPolicy,
        DateTimeOffset? retainUntil,
        bool legalHold) =>
        ProjectId == projectId &&
        OwnerType == ownerType &&
        OwnerId == ownerId &&
        string.Equals(OriginalFileName, originalFileName.Trim(), StringComparison.Ordinal) &&
        string.Equals(ContentType, contentType.Trim(), StringComparison.OrdinalIgnoreCase) &&
        SizeBytes == sizeBytes &&
        string.Equals(Sha256, sha256.Trim(), StringComparison.OrdinalIgnoreCase) &&
        Classification == classification &&
        RetentionPolicy == retentionPolicy &&
        RetainUntil == NormalizeRetention(retentionPolicy, retainUntil, CreatedAt) &&
        LegalHold == legalHold;

    public static bool RequiresProject(DocumentOwnerType ownerType) => ownerType is
        DocumentOwnerType.ProjectGeneral or
        DocumentOwnerType.ProjectChat or
        DocumentOwnerType.ReportOutput or
        DocumentOwnerType.TechnicalDocument;

    private void RequireRevision(long baseRevision)
    {
        if (baseRevision != Revision)
        {
            throw new DomainRuleException(
                "documents.revision.conflict",
                "The document changed before this operation was applied.");
        }
    }

    private static DateTimeOffset? NormalizeRetention(
        DocumentRetentionPolicy policy,
        DateTimeOffset? retainUntil,
        DateTimeOffset effectiveAt)
    {
        if (policy == DocumentRetentionPolicy.Permanent)
        {
            if (retainUntil.HasValue)
            {
                throw new DomainRuleException(
                    "documents.retention.permanent_date",
                    "Permanent retention cannot declare an expiry date.");
            }

            return null;
        }

        var minimum = policy == DocumentRetentionPolicy.LongTerm
            ? effectiveAt.AddYears(10)
            : effectiveAt.AddYears(3);
        if (retainUntil.HasValue && retainUntil.Value < minimum)
        {
            throw new DomainRuleException(
                "documents.retention.too_short",
                "The retention date is shorter than the selected policy allows.");
        }

        return retainUntil ?? minimum;
    }

    private static string Required(string value, int maximumLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleException(errorCode, "A value is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(errorCode, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, maximumLength, errorCode);
    }
}

public enum DocumentOwnerType
{
    ProjectGeneral = 1,
    ProjectChat = 2,
    ReportOutput = 3,
    TechnicalDocument = 4,
    MemberProfile = 5,
    LoginExperience = 6
}

public enum DocumentClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

public enum DocumentRetentionPolicy
{
    Standard = 1,
    LongTerm = 2,
    Permanent = 3
}

public enum DocumentAssetStatus
{
    PendingUpload = 1,
    Quarantined = 2,
    Released = 3,
    Rejected = 4,
    Deleted = 5
}

public enum DocumentScanVerdict
{
    Pending = 1,
    Clean = 2,
    Infected = 3,
    Failed = 4
}
