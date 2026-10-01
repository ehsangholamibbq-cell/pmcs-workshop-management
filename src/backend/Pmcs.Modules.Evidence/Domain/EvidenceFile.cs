using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Evidence.Domain;

public sealed class EvidenceFile : AggregateRoot
{
    public const long MaximumSizeBytes = 25L * 1024L * 1024L;
    public const int SessionLifetimeHours = 24;

    private EvidenceFile()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid DailyReportId { get; private set; }

    public Guid? DailyFactId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Sha256 { get; private set; } = string.Empty;

    public string ObjectKey { get; private set; } = string.Empty;

    public Guid? SourceMessageId { get; private set; }

    public Guid? SourceDocumentId { get; private set; }

    public int? SourceDocumentVersion { get; private set; }

    public EvidenceFileStatus Status { get; private set; }

    public DateTimeOffset? CapturedAtDevice { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UploadExpiresAt { get; private set; }

    public DateTimeOffset? UploadedAt { get; private set; }

    public string? StorageETag { get; private set; }

    public static EvidenceFile CreatePending(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid dailyReportId,
        Guid? dailyFactId,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256,
        string objectKey,
        DateTimeOffset? capturedAtDevice,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            dailyReportId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("evidence.identity.required", "Evidence, tenant, project, report and creator ids are required.");
        }

        if (sizeBytes <= 0 || sizeBytes > MaximumSizeBytes)
        {
            throw new DomainRuleException("evidence.size.invalid", $"Evidence must be between 1 and {MaximumSizeBytes} bytes.");
        }

        var normalizedContentType = Required(contentType, 120, "evidence.content_type.invalid").ToLowerInvariant();
        if (!EvidenceContentPolicy.IsAllowedContentType(normalizedContentType))
        {
            throw new DomainRuleException("evidence.content_type.unsupported", "Only supported images and PDF documents can be uploaded.");
        }

        var normalizedHash = Required(sha256, 64, "evidence.sha256.invalid").ToLowerInvariant();
        if (normalizedHash.Length != 64 || normalizedHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainRuleException("evidence.sha256.invalid", "SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        return new EvidenceFile
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            DailyReportId = dailyReportId,
            DailyFactId = dailyFactId,
            OriginalFileName = Required(originalFileName, 255, "evidence.file_name.invalid"),
            ContentType = normalizedContentType,
            SizeBytes = sizeBytes,
            Sha256 = normalizedHash,
            ObjectKey = Required(objectKey, 700, "evidence.object_key.invalid"),
            Status = EvidenceFileStatus.PendingUpload,
            CapturedAtDevice = capturedAtDevice,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UploadExpiresAt = createdAt.AddHours(SessionLifetimeHours)
        };
    }

    public static EvidenceFile CreateFromReleasedChat(
        Guid id, Guid tenantId, Guid projectId, Guid dailyReportId,
        Guid? dailyFactId, Guid messageId, Guid documentId, int documentVersion,
        string originalFileName, string contentType, long sizeBytes, string sha256,
        Guid createdBy, DateTimeOffset createdAt)
    {
        if (messageId == Guid.Empty || documentId == Guid.Empty || documentVersion < 1)
            throw new DomainRuleException("evidence.chat_source.invalid", "A released chat source is required.");
        var evidence = CreatePending(id, tenantId, projectId, dailyReportId, dailyFactId,
            originalFileName, contentType, sizeBytes, sha256,
            $"pmcs/converted-chat/{id:N}", null, createdBy, createdAt);
        evidence.SourceMessageId = messageId;
        evidence.SourceDocumentId = documentId;
        evidence.SourceDocumentVersion = documentVersion;
        evidence.MarkUploaded($"sha256:{evidence.Sha256}", createdAt);
        return evidence;
    }

    public void RenewUploadSession(DateTimeOffset renewedAt)
    {
        if (Status != EvidenceFileStatus.PendingUpload)
        {
            throw new DomainRuleException("evidence.upload.invalid_state", "Only pending evidence can receive a renewed upload session.");
        }

        UploadExpiresAt = renewedAt.AddHours(SessionLifetimeHours);
        AdvanceRevision();
    }

    public void MarkUploaded(string storageETag, DateTimeOffset uploadedAt)
    {
        if (Status == EvidenceFileStatus.Uploaded)
        {
            return;
        }

        if (Status != EvidenceFileStatus.PendingUpload)
        {
            throw new DomainRuleException("evidence.upload.invalid_state", "Only pending evidence can be marked uploaded.");
        }

        Status = EvidenceFileStatus.Uploaded;
        StorageETag = Required(storageETag, 200, "evidence.storage_etag.invalid");
        UploadedAt = uploadedAt;
        AdvanceRevision();
    }

    public bool MatchesUploadIntent(
        Guid dailyReportId,
        Guid? dailyFactId,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string sha256) =>
        DailyReportId == dailyReportId &&
        DailyFactId == dailyFactId &&
        string.Equals(OriginalFileName, originalFileName.Trim(), StringComparison.Ordinal) &&
        string.Equals(ContentType, contentType.Trim(), StringComparison.OrdinalIgnoreCase) &&
        SizeBytes == sizeBytes &&
        string.Equals(Sha256, sha256.Trim(), StringComparison.OrdinalIgnoreCase);

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
}

public enum EvidenceFileStatus
{
    PendingUpload = 1,
    Uploaded = 2,
    Rejected = 3,
    Deleted = 4
}
