using Pmcs.Modules.ActionControl.Domain;

namespace Pmcs.Modules.ActionControl.Endpoints;

public sealed record CreateActionFromAttentionRequest(
    Guid ClientGeneratedId,
    Guid AssigneeUserId,
    DateOnly DueDate,
    ActionPriority Priority,
    string? Title,
    string? Description);

public sealed record DismissAttentionRequest(string Reason);

public sealed record TransitionActionRequest(long BaseRevision, ManagementActionStatus TargetStatus);

public sealed record ManagementActionResponse(
    Guid Id,
    Guid ProjectId,
    Guid SourceFactId,
    string Title,
    string? Description,
    Guid AssigneeUserId,
    string AssigneeDisplayName,
    DateOnly DueDate,
    ActionPriority Priority,
    ManagementActionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastChangedAt,
    DateTimeOffset? CompletedAt,
    long Revision)
{
    internal static ManagementActionResponse From(ManagementAction action) => new(
        action.Id,
        action.ProjectId,
        action.SourceFactId,
        action.Title,
        action.Description,
        action.AssigneeUserId,
        action.AssigneeDisplayName,
        action.DueDate,
        action.Priority,
        action.Status,
        action.CreatedAt,
        action.LastChangedAt,
        action.CompletedAt,
        action.Revision);
}

public sealed record AttentionDismissalResponse(
    Guid SourceFactId,
    AttentionDispositionKind Kind,
    string Reason,
    Guid DecidedBy,
    DateTimeOffset DecidedAt);
