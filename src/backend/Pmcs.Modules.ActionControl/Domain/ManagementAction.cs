using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.ActionControl.Domain;

public sealed class ManagementAction : AggregateRoot
{
    private ManagementAction()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid SourceFactId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid AssigneeUserId { get; private set; }

    public string AssigneeDisplayName { get; private set; } = string.Empty;

    public DateOnly DueDate { get; private set; }

    public ActionPriority Priority { get; private set; }

    public ManagementActionStatus Status { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? LastChangedBy { get; private set; }

    public DateTimeOffset? LastChangedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static ManagementAction Create(
        Guid id,
        Guid tenantId,
        Guid projectId,
        Guid sourceFactId,
        string title,
        string? description,
        Guid assigneeUserId,
        string assigneeDisplayName,
        DateOnly dueDate,
        ActionPriority priority,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty || sourceFactId == Guid.Empty ||
            assigneeUserId == Guid.Empty || createdBy == Guid.Empty)
        {
            throw new DomainRuleException("action.identity.required", "Action, tenant, project, source, assignee and creator ids are required.");
        }

        if (dueDate == default)
        {
            throw new DomainRuleException("action.due_date.required", "Action due date is required.");
        }

        if (!Enum.IsDefined(priority))
        {
            throw new DomainRuleException("action.priority.invalid", "Action priority is invalid.");
        }

        return new ManagementAction
        {
            Id = id,
            TenantId = tenantId,
            ProjectId = projectId,
            SourceFactId = sourceFactId,
            Title = Required(title, 240, "action.title.invalid"),
            Description = Optional(description, 2_000, "action.description.too_long"),
            AssigneeUserId = assigneeUserId,
            AssigneeDisplayName = Required(assigneeDisplayName, 200, "action.assignee_name.invalid"),
            DueDate = dueDate,
            Priority = priority,
            Status = ManagementActionStatus.Open,
            CreatedBy = createdBy,
            CreatedAt = createdAt
        };
    }

    public void Transition(
        long baseRevision,
        ManagementActionStatus targetStatus,
        Guid changedBy,
        DateTimeOffset changedAt)
    {
        if (Revision != baseRevision)
        {
            throw new DomainRuleException("action.revision.conflict", "The action changed after it was loaded.");
        }

        if (changedBy == Guid.Empty || !Enum.IsDefined(targetStatus))
        {
            throw new DomainRuleException("action.transition.invalid", "A valid actor and target status are required.");
        }

        if (!AllowedTargets(Status).Contains(targetStatus))
        {
            throw new DomainRuleException("action.transition.invalid_state", $"Action cannot move from {Status} to {targetStatus}.");
        }

        Status = targetStatus;
        LastChangedBy = changedBy;
        LastChangedAt = changedAt;
        CompletedAt = targetStatus == ManagementActionStatus.Done ? changedAt : null;
        AdvanceRevision();
    }

    private static HashSet<ManagementActionStatus> AllowedTargets(ManagementActionStatus status) => status switch
    {
        ManagementActionStatus.Open => new HashSet<ManagementActionStatus>
        {
            ManagementActionStatus.InProgress,
            ManagementActionStatus.Blocked,
            ManagementActionStatus.Done,
            ManagementActionStatus.Cancelled
        },
        ManagementActionStatus.InProgress => new HashSet<ManagementActionStatus>
        {
            ManagementActionStatus.Blocked,
            ManagementActionStatus.Done,
            ManagementActionStatus.Cancelled
        },
        ManagementActionStatus.Blocked => new HashSet<ManagementActionStatus>
        {
            ManagementActionStatus.InProgress,
            ManagementActionStatus.Done,
            ManagementActionStatus.Cancelled
        },
        _ => new HashSet<ManagementActionStatus>()
    };

    private static string Required(string value, int maximumLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleException(errorCode, "A value is required.");
        }

        return Optional(value, maximumLength, errorCode)!;
    }

    private static string? Optional(string? value, int maximumLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(errorCode, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }
}

public enum ActionPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ManagementActionStatus
{
    Open = 1,
    InProgress = 2,
    Blocked = 3,
    Done = 4,
    Cancelled = 5
}
