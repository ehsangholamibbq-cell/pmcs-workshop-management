using System.Data.Common;

namespace Pmcs.BuildingBlocks.Application;

public sealed record IdempotencyReceipt(
    Guid TenantId,
    string Key,
    string Operation,
    string RequestHash,
    int StatusCode,
    string ResponseBody,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record TransactionalSideEffectBatch(
    AuditEntry Audit,
    OutboxEnvelope Outbox,
    IdempotencyReceipt Idempotency);

public sealed record TransactionalEventBatch(
    AuditEntry Audit,
    OutboxEnvelope Outbox);

public interface ITransactionalSideEffectWriter
{
    Task WriteAuditAsync(
        DbConnection connection,
        DbTransaction transaction,
        AuditEntry audit,
        CancellationToken cancellationToken = default);

    Task WriteEventAsync(
        DbConnection connection,
        DbTransaction transaction,
        TransactionalEventBatch batch,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        DbConnection connection,
        DbTransaction transaction,
        TransactionalSideEffectBatch batch,
        CancellationToken cancellationToken = default);
}
