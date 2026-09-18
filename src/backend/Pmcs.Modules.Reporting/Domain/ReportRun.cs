using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public sealed class ReportRun : AggregateRoot
{
    private ReportRun()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public string DefinitionCode { get; private set; } = string.Empty;
    public Guid TemplateVersionId { get; private set; }
    public string TemplateVersion { get; private set; } = string.Empty;
    public string ParametersJson { get; private set; } = string.Empty;
    public string ParametersHash { get; private set; } = string.Empty;
    public string RequestedFormatsJson { get; private set; } = string.Empty;
    public DateTimeOffset AsOfUtc { get; private set; }
    public string ProjectTimeZone { get; private set; } = string.Empty;
    public Guid RequestedBy { get; private set; }
    public string RequestPermissionSnapshotJson { get; private set; } = string.Empty;
    public string? ProcessingPermissionSnapshotJson { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string IdempotencyKeyHash { get; private set; } = string.Empty;
    public ReportRunStatus Status { get; private set; }
    public ReportPipelineStage PipelineStage { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? DiagnosticCode { get; private set; }
    public string? DiagnosticDetail { get; private set; }
    public Guid? SnapshotId { get; private set; }
    public int OutputCount { get; private set; }

    public static ReportRun Queue(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid definitionId,
        string definitionCode,
        Guid templateVersionId,
        string templateVersion,
        string parametersJson,
        string parametersHash,
        string requestedFormatsJson,
        DateTimeOffset asOfUtc,
        string projectTimeZone,
        Guid requestedBy,
        string requestPermissionSnapshotJson,
        string correlationId,
        string idempotencyKeyHash,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            definitionId == Guid.Empty || templateVersionId == Guid.Empty || requestedBy == Guid.Empty)
        {
            throw new DomainRuleException("reporting.run.identity.required", "Report run identities are required.");
        }

        return new ReportRun
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            DefinitionId = definitionId,
            DefinitionCode = Required(definitionCode, 120, "reporting.definition.invalid"),
            TemplateVersionId = templateVersionId,
            TemplateVersion = Required(templateVersion, 40, "reporting.template.invalid"),
            ParametersJson = RequiredJson(parametersJson, "reporting.parameters.invalid"),
            ParametersHash = RequiredHash(parametersHash, "reporting.parameters.invalid"),
            RequestedFormatsJson = RequiredJson(requestedFormatsJson, "reporting.format.unsupported"),
            AsOfUtc = asOfUtc.ToUniversalTime(),
            ProjectTimeZone = Required(projectTimeZone, 120, "reporting.project.time_zone.invalid"),
            RequestedBy = requestedBy,
            RequestPermissionSnapshotJson = RequiredJson(
                requestPermissionSnapshotJson,
                "reporting.permission_snapshot.invalid"),
            CorrelationId = Required(correlationId, 160, "reporting.correlation.invalid"),
            IdempotencyKeyHash = RequiredHash(idempotencyKeyHash, "reporting.idempotency.invalid"),
            Status = ReportRunStatus.Queued,
            PipelineStage = ReportPipelineStage.Queued,
            CreatedAt = createdAt.ToUniversalTime(),
            Revision = 1
        };
    }

    public void StartAttempt(string processingPermissionSnapshotJson, DateTimeOffset startedAt)
    {
        if (Status is not (ReportRunStatus.Queued or ReportRunStatus.Processing) ||
            PipelineStage == ReportPipelineStage.SnapshotReady)
        {
            throw new DomainRuleException("reporting.run.invalid_state", "Report run cannot start snapshot processing.");
        }

        Status = ReportRunStatus.Processing;
        PipelineStage = ReportPipelineStage.BuildingSnapshot;
        ProcessingPermissionSnapshotJson = RequiredJson(
            processingPermissionSnapshotJson,
            "reporting.permission_snapshot.invalid");
        ClaimedAt = startedAt.ToUniversalTime();
        StartedAt ??= startedAt.ToUniversalTime();
        NextAttemptAt = null;
        DiagnosticCode = null;
        DiagnosticDetail = null;
        AttemptCount++;
        AdvanceRevision();
    }

    public void AttachSnapshot(
        Guid snapshotId,
        string processingPermissionSnapshotJson,
        DateTimeOffset builtAt)
    {
        if (Status != ReportRunStatus.Processing || PipelineStage != ReportPipelineStage.BuildingSnapshot ||
            SnapshotId.HasValue || snapshotId == Guid.Empty)
        {
            throw new DomainRuleException("reporting.run.invalid_state", "Report run cannot accept a snapshot.");
        }

        SnapshotId = snapshotId;
        ProcessingPermissionSnapshotJson = RequiredJson(
            processingPermissionSnapshotJson,
            "reporting.permission_snapshot.invalid");
        PipelineStage = ReportPipelineStage.SnapshotReady;
        ClaimedAt = builtAt.ToUniversalTime();
        AdvanceRevision();
    }

    public void RecordProcessingPermissionSnapshot(string processingPermissionSnapshotJson)
    {
        if (Status != ReportRunStatus.Processing || PipelineStage != ReportPipelineStage.BuildingSnapshot ||
            SnapshotId.HasValue)
        {
            throw new DomainRuleException(
                "reporting.run.invalid_state",
                "Processing permission evidence cannot be recorded for this run.");
        }

        ProcessingPermissionSnapshotJson = RequiredJson(
            processingPermissionSnapshotJson,
            "reporting.permission_snapshot.invalid");
        AdvanceRevision();
    }

    public void Requeue(string diagnosticCode, DateTimeOffset nextAttemptAt)
    {
        if (Status != ReportRunStatus.Processing || PipelineStage != ReportPipelineStage.BuildingSnapshot ||
            SnapshotId.HasValue)
        {
            throw new DomainRuleException("reporting.run.invalid_state", "Report run cannot be requeued.");
        }

        Status = ReportRunStatus.Queued;
        PipelineStage = ReportPipelineStage.Queued;
        DiagnosticCode = Required(diagnosticCode, 120, "reporting.diagnostic.invalid");
        DiagnosticDetail = null;
        NextAttemptAt = nextAttemptAt.ToUniversalTime();
        ClaimedAt = null;
        AdvanceRevision();
    }

    public void RequeueRendering(string diagnosticCode, DateTimeOffset nextAttemptAt)
    {
        if (Status != ReportRunStatus.Processing ||
            PipelineStage is not (ReportPipelineStage.SnapshotReady or ReportPipelineStage.Rendering) ||
            !SnapshotId.HasValue || OutputCount > 0)
        {
            throw new DomainRuleException("reporting.run.invalid_state", "Report rendering cannot be requeued.");
        }

        PipelineStage = ReportPipelineStage.SnapshotReady;
        DiagnosticCode = Required(diagnosticCode, 120, "reporting.diagnostic.invalid");
        DiagnosticDetail = null;
        NextAttemptAt = nextAttemptAt.ToUniversalTime();
        ClaimedAt = null;
        AdvanceRevision();
    }

    public void RetryFailed(DateTimeOffset retriedAt)
    {
        if (Status != ReportRunStatus.Failed || OutputCount > 0)
        {
            throw new DomainRuleException("reporting.run.not_retryable", "Report run is not retryable.");
        }

        Status = SnapshotId.HasValue ? ReportRunStatus.Processing : ReportRunStatus.Queued;
        PipelineStage = SnapshotId.HasValue
            ? ReportPipelineStage.SnapshotReady
            : ReportPipelineStage.Queued;
        ClaimedAt = null;
        CompletedAt = null;
        NextAttemptAt = retriedAt.ToUniversalTime();
        DiagnosticCode = "reporting.retry.requested";
        DiagnosticDetail = null;
        AdvanceRevision();
    }

    public void BeginRendering(DateTimeOffset startedAt)
    {
        if (Status != ReportRunStatus.Processing || PipelineStage != ReportPipelineStage.SnapshotReady ||
            !SnapshotId.HasValue)
        {
            throw new DomainRuleException("reporting.run.invalid_state", "Report run cannot start rendering.");
        }

        PipelineStage = ReportPipelineStage.Rendering;
        ClaimedAt = startedAt.ToUniversalTime();
        AdvanceRevision();
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is ReportRunStatus.Succeeded or ReportRunStatus.Failed or ReportRunStatus.Cancelled ||
            PipelineStage == ReportPipelineStage.Rendering || OutputCount > 0)
        {
            throw new DomainRuleException("reporting.run.already_final", "A final report run cannot be cancelled.");
        }

        Status = ReportRunStatus.Cancelled;
        PipelineStage = ReportPipelineStage.Cancelled;
        CompletedAt = cancelledAt.ToUniversalTime();
        NextAttemptAt = null;
        AdvanceRevision();
    }

    public void Fail(
        string diagnosticCode,
        string? diagnosticDetail,
        DateTimeOffset failedAt,
        string? processingPermissionSnapshotJson = null)
    {
        if (Status is ReportRunStatus.Succeeded or ReportRunStatus.Failed or ReportRunStatus.Cancelled)
        {
            throw new DomainRuleException("reporting.run.already_final", "A final report run cannot fail.");
        }

        Status = ReportRunStatus.Failed;
        PipelineStage = ReportPipelineStage.Failed;
        DiagnosticCode = Required(diagnosticCode, 120, "reporting.diagnostic.invalid");
        DiagnosticDetail = Optional(diagnosticDetail, 500, "reporting.diagnostic.invalid");
        if (!string.IsNullOrWhiteSpace(processingPermissionSnapshotJson))
        {
            ProcessingPermissionSnapshotJson = RequiredJson(
                processingPermissionSnapshotJson,
                "reporting.permission_snapshot.invalid");
        }
        CompletedAt = failedAt.ToUniversalTime();
        NextAttemptAt = null;
        AdvanceRevision();
    }

    public void Complete(int outputCount, DateTimeOffset completedAt)
    {
        if (Status != ReportRunStatus.Processing || PipelineStage != ReportPipelineStage.Rendering ||
            !SnapshotId.HasValue || outputCount <= 0)
        {
            throw new DomainRuleException("reporting.run.invalid_state", "Report run cannot be completed.");
        }

        OutputCount = outputCount;
        Status = ReportRunStatus.Succeeded;
        PipelineStage = ReportPipelineStage.Complete;
        CompletedAt = completedAt.ToUniversalTime();
        NextAttemptAt = null;
        AdvanceRevision();
    }

    private static string Required(string value, int maximumLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, "A required reporting value is missing or too long.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Required(value, maximumLength, code);
    }

    private static string RequiredJson(string value, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 2 || (normalized[0] != '{' && normalized[0] != '['))
        {
            throw new DomainRuleException(code, "A canonical JSON value is required.");
        }

        return normalized;
    }

    private static string RequiredHash(string value, string code)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainRuleException(code, "A lowercase SHA-256 value is required.");
        }

        return normalized;
    }
}
