namespace Pmcs.Modules.WorkManagement.Endpoints;

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
