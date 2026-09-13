using System.Data.Common;

namespace Pmcs.BuildingBlocks.Application;

public sealed record InAppNotificationDraft(
    Guid NotificationId,
    Guid TenantId,
    Guid ProjectId,
    Guid RecipientUserId,
    string DeduplicationKey,
    string Category,
    string Title,
    string Body,
    string TargetType,
    Guid TargetId,
    DateTimeOffset OccurredAt);

public interface ITransactionalNotificationWriter
{
    Task WriteAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyCollection<InAppNotificationDraft> notifications,
        CancellationToken cancellationToken = default);
}
