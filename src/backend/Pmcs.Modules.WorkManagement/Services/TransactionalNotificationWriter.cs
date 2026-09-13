using System.Data.Common;
using Npgsql;
using NpgsqlTypes;
using Pmcs.BuildingBlocks.Application;

namespace Pmcs.Modules.WorkManagement.Services;

internal sealed class TransactionalNotificationWriter : ITransactionalNotificationWriter
{
    public async Task WriteAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyCollection<InAppNotificationDraft> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        if (connection is not NpgsqlConnection postgresConnection ||
            transaction is not NpgsqlTransaction postgresTransaction)
        {
            throw new InvalidOperationException("Transactional notifications require an Npgsql transaction.");
        }

        foreach (var notification in notifications)
        {
            Validate(notification);
            await using var command = new NpgsqlCommand(
                """
                insert into work_management.notifications(
                    id, tenant_id, project_id, recipient_user_id, deduplication_key,
                    category, title, body, target_type, target_id, occurred_at,
                    read_at, acknowledged_at, revision)
                values (
                    @id, @tenant_id, @project_id, @recipient_user_id, @deduplication_key,
                    @category, @title, @body, @target_type, @target_id, @occurred_at,
                    null, null, 1)
                on conflict (tenant_id, recipient_user_id, deduplication_key) do nothing;
                """,
                postgresConnection,
                postgresTransaction);
            command.Parameters.AddWithValue("id", notification.NotificationId);
            command.Parameters.AddWithValue("tenant_id", notification.TenantId);
            command.Parameters.AddWithValue("project_id", notification.ProjectId);
            command.Parameters.AddWithValue("recipient_user_id", notification.RecipientUserId);
            command.Parameters.AddWithValue("deduplication_key", notification.DeduplicationKey.Trim());
            command.Parameters.AddWithValue("category", notification.Category.Trim());
            command.Parameters.AddWithValue("title", notification.Title.Trim());
            command.Parameters.AddWithValue("body", NpgsqlDbType.Varchar, notification.Body.Trim());
            command.Parameters.AddWithValue("target_type", notification.TargetType.Trim());
            command.Parameters.AddWithValue("target_id", notification.TargetId);
            command.Parameters.AddWithValue("occurred_at", notification.OccurredAt);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void Validate(InAppNotificationDraft notification)
    {
        if (notification.NotificationId == Guid.Empty || notification.TenantId == Guid.Empty ||
            notification.ProjectId == Guid.Empty || notification.RecipientUserId == Guid.Empty ||
            notification.TargetId == Guid.Empty)
        {
            throw new InvalidOperationException("Notification identities are required.");
        }

        ValidateText(notification.DeduplicationKey, 240, nameof(notification.DeduplicationKey));
        ValidateText(notification.Category, 80, nameof(notification.Category));
        ValidateText(notification.Title, 240, nameof(notification.Title));
        ValidateText(notification.Body, 2_000, nameof(notification.Body));
        ValidateText(notification.TargetType, 80, nameof(notification.TargetType));
    }

    private static void ValidateText(string value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw new InvalidOperationException($"{name} is required and must be at most {maximumLength} characters.");
        }
    }
}
