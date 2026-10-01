using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Contracts;

public sealed record ReportingCatalogEntry(
    string Code,
    string Title,
    string Description,
    ReportDefinitionScope Scope,
    ReportClassification Classification,
    string ParameterSchemaVersion,
    string TemplateVersion,
    IReadOnlyCollection<ReportFormat> SupportedFormats,
    IReadOnlyCollection<string> RequiredSourcePermissions);

public sealed record ReportingRunStatusRecord(
    Guid Id,
    Guid ProjectId,
    string DefinitionCode,
    string TemplateVersion,
    ReportRunStatus Status,
    ReportPipelineStage PipelineStage,
    ReportDataStatus? DataStatus,
    DateTimeOffset AsOfUtc,
    IReadOnlyCollection<ReportFormat> RequestedFormats,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? DiagnosticCode,
    string? SnapshotHash,
    IReadOnlyCollection<ReportingOutputMetadataRecord> Outputs);

public sealed record ReportingOutputMetadataRecord(
    Guid Id,
    Guid RunId,
    Guid ProjectId,
    ReportFormat Format,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string VerificationCode,
    ReportClassification Classification,
    ReportOutputArchiveState ArchiveState);

public interface IReportingReadService
{
    Task<IReadOnlyCollection<ReportingCatalogEntry>> ListCatalogAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ReportingRunStatusRecord>> ListRunsAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        ReportRunStatus? status = null,
        string? definitionCode = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<ReportingRunStatusRecord?> FindRunAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<ReportingOutputMetadataRecord?> FindOutputAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid projectId,
        Guid outputId,
        CancellationToken cancellationToken = default);
}
