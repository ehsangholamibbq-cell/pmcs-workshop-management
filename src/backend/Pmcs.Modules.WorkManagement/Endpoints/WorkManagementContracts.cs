namespace Pmcs.Modules.WorkManagement.Contracts;

public sealed record NotificationReceiptRequest(long BaseRevision);

public sealed record InAppNotificationResponse(
    Guid Id,
    Guid ProjectId,
    string Category,
    string Title,
    string Body,
    string TargetType,
    Guid TargetId,
    DateTimeOffset OccurredAt,
    DateTimeOffset? ReadAt,
    DateTimeOffset? AcknowledgedAt,
    long Revision);

public sealed record MyWorkItemResponse(
    string Id,
    string Kind,
    string Title,
    string? Description,
    DateOnly? DueDate,
    DateOnly? ReferenceDate,
    string Priority,
    string Status,
    string TargetType,
    Guid TargetId,
    DateTimeOffset ChangedAt,
    long Revision,
    bool IsOverdue);

public sealed record MyWorkResponse(
    DateTimeOffset CalculatedAt,
    int UnreadNotificationCount,
    IReadOnlyCollection<MyWorkItemResponse> Items);

public enum WorkManagementQueryStatus
{
    Success = 1,
    Forbidden = 2,
    ProjectNotFound = 3
}

public sealed record WorkManagementQueryResult<T>(
    WorkManagementQueryStatus Status,
    T? Value);

public interface IWorkManagementQueryService
{
    Task<WorkManagementQueryResult<MyWorkResponse>> GetMyWorkAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<WorkManagementQueryResult<IReadOnlyCollection<InAppNotificationResponse>>> ListNotificationsAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        bool unreadOnly,
        CancellationToken cancellationToken = default);
}
