namespace Pmcs.Modules.Intelligence.Domain;

internal enum IntelligenceRunStatus { Requested, Validated, Running, Completed, Failed, Cancelled }

// The request text, tool payload, provider response and credentials never enter this store.
internal sealed class IntelligenceReferenceRun
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public DateTimeOffset SessionExpiresAt { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public Guid ProfileVersionId { get; private set; }
    public int ProfileVersion { get; private set; }
    public Guid ModelCatalogId { get; private set; }
    public int ModelVersion { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public string PolicyVersion { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public IntelligenceRunStatus Status { get; private set; }
    public string? ToolId { get; private set; }
    public string? ToolDecision { get; private set; }
    public bool Fallback { get; private set; }
    public string? FallbackReason { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public long CostMicrounits { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? ValidatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long? LatencyMilliseconds => StartedAt.HasValue && CompletedAt.HasValue
        ? Math.Max(0, (long)(CompletedAt.Value - StartedAt.Value).TotalMilliseconds) : null;
    public long Revision { get; private set; }

    private IntelligenceReferenceRun() { }

    internal static IntelligenceReferenceRun Request(Guid id, Guid tenantId, Guid projectId,
        Guid actorId, ModelExecutionProfile profile, ModelCatalogEntry model,
        string requestHash, DateTimeOffset now)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty ||
            actorId == Guid.Empty || profile.TenantId != tenantId ||
            requestHash.Length != 64 || !requestHash.All(Uri.IsHexDigit) ||
            !profile.AllowedModelIds.Contains(model.Id) ||
            profile.ProjectIds.Count > 0 && !profile.ProjectIds.Contains(projectId))
            throw new ArgumentException("Invalid reference Run identity or policy.");
        return new IntelligenceReferenceRun
        {
            Id = id, SessionId = Guid.NewGuid(), SessionExpiresAt = now.AddMinutes(10),
            TenantId = tenantId, ProjectId = projectId, RequestedBy = actorId,
            ProfileVersionId = profile.Id, ProfileVersion = profile.Version,
            ModelCatalogId = model.Id, ModelVersion = model.Version,
            Provider = model.Provider, Model = model.Model,
            PromptVersion = profile.PromptVersion, PolicyVersion = profile.PolicyVersion,
            RequestHash = requestHash, Status = IntelligenceRunStatus.Requested,
            RequestedAt = now, Revision = 1
        };
    }

    internal void Validate(DateTimeOffset now)
    {
        Transition(IntelligenceRunStatus.Requested, IntelligenceRunStatus.Validated, now);
        ValidatedAt = now;
    }

    internal void Start(DateTimeOffset now)
    {
        Transition(IntelligenceRunStatus.Validated, IntelligenceRunStatus.Running, now);
        StartedAt = now;
    }

    internal void RecordTool(string toolId, string decision)
    {
        if (Status != IntelligenceRunStatus.Running || ToolId is not null ||
            string.IsNullOrWhiteSpace(toolId) || toolId.Length > 100 ||
            decision is not ("Allowed" or "Denied"))
            throw new InvalidOperationException("Invalid tool decision.");
        ToolId = toolId;
        ToolDecision = decision;
        Revision++;
    }

    internal void SelectFallback(ModelCatalogEntry model, string reason)
    {
        if (Status != IntelligenceRunStatus.Running || Fallback ||
            reason is not ("ai.provider.timeout" or "ai.provider.unavailable" or
                "ai.provider.invalid_response"))
            throw new InvalidOperationException("Invalid fallback transition.");
        ModelCatalogId = model.Id;
        ModelVersion = model.Version;
        Provider = model.Provider;
        Model = model.Model;
        Fallback = true;
        FallbackReason = reason;
        Revision++;
    }

    internal void Complete(int inputTokens, int outputTokens, long costMicrounits,
        DateTimeOffset now)
    {
        if (inputTokens < 0 || outputTokens < 0 || costMicrounits < 0)
            throw new ArgumentException("Usage must be non-negative.");
        Transition(IntelligenceRunStatus.Running, IntelligenceRunStatus.Completed, now);
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostMicrounits = costMicrounits;
        CompletedAt = now;
    }

    internal void Fail(string code, DateTimeOffset now)
    {
        if (Status is IntelligenceRunStatus.Completed or IntelligenceRunStatus.Failed or
            IntelligenceRunStatus.Cancelled || string.IsNullOrWhiteSpace(code) || code.Length > 120)
            throw new InvalidOperationException("Invalid failure transition.");
        Status = IntelligenceRunStatus.Failed;
        ErrorCode = code;
        CompletedAt = now;
        Revision++;
    }

    internal void Cancel(DateTimeOffset now)
    {
        if (Status is IntelligenceRunStatus.Completed or IntelligenceRunStatus.Failed or
            IntelligenceRunStatus.Cancelled)
            throw new InvalidOperationException("Invalid cancellation transition.");
        Status = IntelligenceRunStatus.Cancelled;
        ErrorCode = "ai.run.cancelled";
        CompletedAt = now;
        Revision++;
    }

    private void Transition(IntelligenceRunStatus expected, IntelligenceRunStatus next,
        DateTimeOffset now)
    {
        if (Status != expected || now < RequestedAt ||
            StartedAt.HasValue && now < StartedAt.Value)
            throw new InvalidOperationException("Invalid Run transition.");
        Status = next;
        Revision++;
    }
}
