using System.Data.Common;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Pmcs.BuildingBlocks.Application;

namespace Pmcs.Modules.Platform.Services;

internal sealed class TransactionalSideEffectWriter : ITransactionalSideEffectWriter
{
    public async Task WriteAsync(
        DbConnection connection,
        DbTransaction transaction,
        TransactionalSideEffectBatch batch,
        CancellationToken cancellationToken = default)
    {
        if (connection is not NpgsqlConnection postgresConnection || transaction is not NpgsqlTransaction postgresTransaction)
        {
            throw new InvalidOperationException("Transactional side effects require an Npgsql connection and transaction.");
        }

        await InsertAuditAsync(postgresConnection, postgresTransaction, batch.Audit, cancellationToken);
        await InsertOutboxAsync(postgresConnection, postgresTransaction, batch.Outbox, cancellationToken);
        await InsertIdempotencyAsync(postgresConnection, postgresTransaction, batch.Idempotency, cancellationToken);
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuditEntry entry,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into foundation.audit_events(
                event_id, tenant_id, project_id, actor_user_id, event_type,
                resource_type, resource_id, occurred_at, data, correlation_id)
            values (
                @event_id, @tenant_id, @project_id, @actor_user_id, @event_type,
                @resource_type, @resource_id, @occurred_at, @data, @correlation_id);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("event_id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant_id", entry.TenantId);
        AddNullable(command, "project_id", NpgsqlDbType.Uuid, entry.ProjectId);
        command.Parameters.AddWithValue("actor_user_id", entry.ActorUserId);
        command.Parameters.AddWithValue("event_type", entry.EventType);
        command.Parameters.AddWithValue("resource_type", entry.ResourceType);
        command.Parameters.AddWithValue("resource_id", entry.ResourceId);
        command.Parameters.AddWithValue("occurred_at", entry.OccurredAt);
        command.Parameters.AddWithValue("data", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(entry.Data));
        AddNullable(command, "correlation_id", NpgsqlDbType.Varchar, entry.CorrelationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OutboxEnvelope envelope,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into foundation.outbox_messages(
                message_id, tenant_id, project_id, event_type, event_version,
                occurred_at, payload, correlation_id, published_at, attempts, last_error)
            values (
                @message_id, @tenant_id, @project_id, @event_type, @event_version,
                @occurred_at, @payload, @correlation_id, null, 0, null);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("message_id", envelope.MessageId);
        command.Parameters.AddWithValue("tenant_id", envelope.TenantId);
        AddNullable(command, "project_id", NpgsqlDbType.Uuid, envelope.ProjectId);
        command.Parameters.AddWithValue("event_type", envelope.EventType);
        command.Parameters.AddWithValue("event_version", envelope.EventVersion);
        command.Parameters.AddWithValue("occurred_at", envelope.OccurredAt);
        command.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, envelope.Payload);
        AddNullable(command, "correlation_id", NpgsqlDbType.Varchar, envelope.CorrelationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertIdempotencyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IdempotencyReceipt receipt,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into foundation.idempotency_records(
                id, tenant_id, key, operation, request_hash, status_code,
                response_body, created_at, expires_at)
            values (
                @id, @tenant_id, @key, @operation, @request_hash, @status_code,
                @response_body, @created_at, @expires_at);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("tenant_id", receipt.TenantId);
        command.Parameters.AddWithValue("key", receipt.Key);
        command.Parameters.AddWithValue("operation", receipt.Operation);
        command.Parameters.AddWithValue("request_hash", receipt.RequestHash);
        command.Parameters.AddWithValue("status_code", receipt.StatusCode);
        command.Parameters.AddWithValue("response_body", NpgsqlDbType.Jsonb, receipt.ResponseBody);
        command.Parameters.AddWithValue("created_at", receipt.CreatedAt);
        command.Parameters.AddWithValue("expires_at", receipt.ExpiresAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullable<T>(
        NpgsqlCommand command,
        string name,
        NpgsqlDbType type,
        T? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type)
        {
            Value = value is null ? DBNull.Value : value
        });
    }
}
