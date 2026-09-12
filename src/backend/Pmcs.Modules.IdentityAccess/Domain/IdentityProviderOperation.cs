using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.IdentityAccess.Domain;

public sealed class IdentityProviderOperation : AggregateRoot
{
    private IdentityProviderOperation()
    {
    }

    private IdentityProviderOperation(
        Guid id,
        Guid tenantId,
        Guid userId,
        IdentityProviderOperationType operationType,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        OperationType = operationType;
        Status = IdentityProviderOperationStatus.Queued;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public IdentityProviderOperationType OperationType { get; private set; }

    public IdentityProviderOperationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastErrorCode { get; private set; }

    public static IdentityProviderOperation Queue(
        Guid id,
        Guid tenantId,
        Guid userId,
        IdentityProviderOperationType operationType,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || userId == Guid.Empty || !Enum.IsDefined(operationType))
        {
            throw new DomainRuleException("identity_operation.invalid", "Identity provider operation is invalid.");
        }

        return new IdentityProviderOperation(id, tenantId, userId, operationType, createdAt);
    }

    public void BeginAttempt()
    {
        if (Status is IdentityProviderOperationStatus.Completed)
        {
            throw new DomainRuleException("identity_operation.completed", "Completed operation cannot be processed again.");
        }

        Status = IdentityProviderOperationStatus.Processing;
        Attempts++;
        NextAttemptAt = null;
        LastErrorCode = null;
        AdvanceRevision();
    }

    public void Complete(DateTimeOffset at)
    {
        Status = IdentityProviderOperationStatus.Completed;
        CompletedAt = at;
        NextAttemptAt = null;
        LastErrorCode = null;
        AdvanceRevision();
    }

    public void ScheduleRetry(string errorCode, DateTimeOffset nextAttemptAt)
    {
        Status = IdentityProviderOperationStatus.RetryScheduled;
        LastErrorCode = Normalize(errorCode);
        NextAttemptAt = nextAttemptAt;
        AdvanceRevision();
    }

    public void Fail(string errorCode)
    {
        Status = IdentityProviderOperationStatus.Failed;
        LastErrorCode = Normalize(errorCode);
        NextAttemptAt = null;
        AdvanceRevision();
    }

    private static string Normalize(string errorCode) =>
        string.IsNullOrWhiteSpace(errorCode) ? "identity_provider.unavailable" : errorCode.Trim()[..Math.Min(120, errorCode.Trim().Length)];
}

public enum IdentityProviderOperationType
{
    Enable = 1,
    DisableAndLogout = 2,
    Logout = 3,
    Delete = 4
}

public enum IdentityProviderOperationStatus
{
    Queued = 1,
    Processing = 2,
    RetryScheduled = 3,
    Completed = 4,
    Failed = 5
}
