namespace Pmcs.Modules.FieldOperations.Contracts;

public interface IProgressEvidenceReportingSource
{
    Task<ProgressEvidenceReportingProjection> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly throughLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default);
}

public static class ProgressEvidenceReportingContract
{
    public const string Version = "pmcs.field-operations.progress-evidence-reporting/v1";
}

public sealed record ProgressEvidenceReportingProjection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly ThroughLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProgressEvidenceReportingClassification Classification,
    IReadOnlyCollection<ProgressEvidenceReportingRoot> Roots);

public sealed record ProgressEvidenceReportingRoot(
    Guid RootReportId,
    DateOnly ReportDate,
    Guid? CurrentOfficialReportId,
    ProgressEvidenceReportingClassification Classification,
    IReadOnlyCollection<ProgressEvidenceReportingVersion> Versions);

public sealed record ProgressEvidenceReportingVersion(
    Guid ReportId,
    Guid RootReportId,
    int VersionNumber,
    DateOnly ReportDate,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt,
    IReadOnlyCollection<ProgressEvidenceReportingFact> Facts);

public sealed record ProgressEvidenceReportingFact(
    Guid FactId,
    Guid? MeasurementItemId,
    decimal? Quantity,
    string? Unit,
    DateTimeOffset CreatedAt);

public enum ProgressEvidenceReportingClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}
