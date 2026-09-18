namespace Pmcs.Modules.Reporting.Domain;

using Pmcs.BuildingBlocks.Domain;

public sealed class ReportOutput
{
    private ReportOutput()
    {
    }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public Guid TemplateVersionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public ReportFormat Format { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public Guid GeneratedDocumentId { get; private set; }
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string VerificationCode { get; private set; } = string.Empty;
    public string ManifestSha256 { get; private set; } = string.Empty;
    public ReportClassification Classification { get; private set; }
    public string RetentionPolicy { get; private set; } = string.Empty;
    public ReportOutputArchiveState ArchiveState { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public static ReportOutput Create(
        Guid id,
        Guid runId,
        Guid snapshotId,
        Guid templateVersionId,
        Guid tenantId,
        Guid projectId,
        ReportFormat format,
        string contentType,
        string fileName,
        Guid generatedDocumentId,
        long sizeBytes,
        string sha256,
        string verificationCode,
        string manifestSha256,
        ReportClassification classification,
        string retentionPolicy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || runId == Guid.Empty || snapshotId == Guid.Empty ||
            templateVersionId == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            generatedDocumentId == Guid.Empty)
        {
            throw new DomainRuleException("reporting.output.identity.required", "Report output identities are required.");
        }

        if (!Enum.IsDefined(format) || !Enum.IsDefined(classification) || sizeBytes <= 0)
        {
            throw new DomainRuleException("reporting.output.metadata.invalid", "Report output metadata is invalid.");
        }

        var normalizedContentType = Required(contentType, 160, "reporting.output.content_type.invalid")
            .ToLowerInvariant();
        var expectedContentType = format switch
        {
            ReportFormat.Pdf => "application/pdf",
            ReportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportFormat.Csv => "text/csv",
            _ => throw new DomainRuleException("reporting.format.unsupported", "Report output format is unsupported.")
        };
        if (!string.Equals(normalizedContentType, expectedContentType, StringComparison.Ordinal))
        {
            throw new DomainRuleException("reporting.output.content_type.invalid", "Content type does not match output format.");
        }

        var normalizedFileName = Required(fileName, 255, "reporting.output.file_name.invalid");
        var expectedExtension = format switch
        {
            ReportFormat.Pdf => ".pdf",
            ReportFormat.Xlsx => ".xlsx",
            ReportFormat.Csv => ".csv",
            _ => string.Empty
        };
        if (!normalizedFileName.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainRuleException("reporting.output.file_name.invalid", "File extension does not match output format.");
        }

        return new ReportOutput
        {
            Id = id,
            RunId = runId,
            SnapshotId = snapshotId,
            TemplateVersionId = templateVersionId,
            TenantId = tenantId,
            ProjectId = projectId,
            Format = format,
            ContentType = normalizedContentType,
            FileName = normalizedFileName,
            GeneratedDocumentId = generatedDocumentId,
            SizeBytes = sizeBytes,
            Sha256 = Hash(sha256, "reporting.output.sha256.invalid"),
            VerificationCode = Required(verificationCode, 80, "reporting.output.verification.invalid"),
            ManifestSha256 = Hash(manifestSha256, "reporting.output.manifest.invalid"),
            Classification = classification,
            RetentionPolicy = Required(retentionPolicy, 40, "reporting.output.retention.invalid"),
            ArchiveState = ReportOutputArchiveState.Active,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (ArchiveState == ReportOutputArchiveState.Archived)
        {
            return;
        }

        if (archivedAt < CreatedAt)
        {
            throw new DomainRuleException("reporting.output.archive_time.invalid", "Archive time cannot precede creation.");
        }

        ArchiveState = ReportOutputArchiveState.Archived;
        ArchivedAt = archivedAt.ToUniversalTime();
    }

    private static string Required(string value, int maximumLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, "A required output value is missing or too long.");
        }

        return normalized;
    }

    private static string Hash(string value, string code)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainRuleException(code, "A lowercase SHA-256 value is required.");
        }

        return normalized;
    }
}
