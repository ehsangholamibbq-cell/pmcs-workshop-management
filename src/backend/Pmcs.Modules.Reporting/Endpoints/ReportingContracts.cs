using System.Text.Json;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Endpoints;

public sealed record CreateReportRunRequest(
    Guid ClientGeneratedId,
    string DefinitionCode,
    string TemplateVersion,
    DateTimeOffset? AsOfUtc,
    ReportFormat[] Formats,
    JsonElement Parameters);

public sealed record DailyReportReportParameters(
    Guid DailyReportId,
    bool IncludeRevisionChain);

public sealed record ReportCatalogDefinitionResponse(
    string Code,
    string Title,
    string Description,
    ReportDefinitionScope Scope,
    ReportClassification Classification,
    string ParameterSchemaVersion,
    string TemplateVersion,
    IReadOnlyCollection<ReportFormat> SupportedFormats,
    IReadOnlyCollection<string> RequiredSourcePermissions,
    IReadOnlyCollection<ReportDataStatus> DataStatuses);

public sealed record ReportOutputMetadataResponse(
    Guid Id,
    ReportFormat Format,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string VerificationCode,
    string DownloadUrl);

public sealed record ReportRunLinks(string Self);

public sealed record ReportRunResponse(
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
    IReadOnlyCollection<ReportOutputMetadataResponse> Outputs,
    ReportRunLinks Links);

internal sealed record ReportPermissionSnapshot(
    string PolicyVersion,
    Guid ActorUserId,
    Guid ProjectId,
    DateTimeOffset EvaluatedAt,
    IReadOnlyCollection<ReportPermissionDecision> Decisions);

internal sealed record ReportPermissionDecision(
    string Operation,
    bool Allowed,
    string Source,
    string Scope,
    string Condition,
    string? DenyReason,
    DateTimeOffset? ExpiresAt);
