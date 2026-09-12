using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Intelligence.Domain;

public enum InsightGenerationStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4
}

public sealed class InsightGenerationRequest
{
    private InsightGenerationRequest()
    {
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid RequestedBy { get; private set; }
    public InsightGenerationStatus Status { get; private set; }
    public Guid? SnapshotId { get; private set; }
    public Guid? InsightId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastErrorCode { get; private set; }
    public long Revision { get; private set; }

    public static InsightGenerationRequest Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid requestedBy,
        DateTimeOffset requestedAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || requestedBy == Guid.Empty)
        {
            throw new ArgumentException("Generation request identifiers are required.");
        }

        return new InsightGenerationRequest
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            RequestedBy = requestedBy,
            Status = InsightGenerationStatus.Pending,
            RequestedAt = requestedAt,
            Revision = 1
        };
    }

    public void Start(DateTimeOffset startedAt)
    {
        if (Status is not (InsightGenerationStatus.Pending or InsightGenerationStatus.Processing))
        {
            throw new DomainRuleException("insight.request.invalid_state", "Generation request cannot be started.");
        }

        Status = InsightGenerationStatus.Processing;
        StartedAt = startedAt;
        NextAttemptAt = null;
        Attempts++;
        LastErrorCode = null;
        Revision++;
    }

    public void Complete(Guid snapshotId, Guid insightId, DateTimeOffset completedAt)
    {
        if (Status != InsightGenerationStatus.Processing)
        {
            throw new DomainRuleException("insight.request.invalid_state", "Generation request is not processing.");
        }

        SnapshotId = snapshotId;
        InsightId = insightId;
        Status = InsightGenerationStatus.Succeeded;
        CompletedAt = completedAt;
        NextAttemptAt = null;
        Revision++;
    }

    public void Fail(string errorCode, DateTimeOffset completedAt)
    {
        if (Status != InsightGenerationStatus.Processing)
        {
            throw new DomainRuleException("insight.request.invalid_state", "Generation request is not processing.");
        }

        var normalized = errorCode.Trim();
        if (normalized.Length == 0 || normalized.Length > 120)
        {
            throw new ArgumentException("A safe failure code is required.", nameof(errorCode));
        }

        Status = InsightGenerationStatus.Failed;
        LastErrorCode = normalized;
        CompletedAt = completedAt;
        NextAttemptAt = null;
        Revision++;
    }
}
