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

public interface ITransactionalSideEffectWriter
{
    Task WriteAsync(
        DbConnection connection,
        DbTransaction transaction,
        TransactionalSideEffectBatch batch,
        CancellationToken cancellationToken = default);
}
