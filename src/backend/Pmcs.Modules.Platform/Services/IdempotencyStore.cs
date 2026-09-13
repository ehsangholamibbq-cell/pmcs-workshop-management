using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Platform.Persistence;

namespace Pmcs.Modules.Platform.Services;

internal sealed class IdempotencyStore(PlatformDbContext dbContext, IClock clock) : IIdempotencyStore
{
    public async Task<IdempotencyReplay?> FindAsync(
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        IdempotencyKeyRules.Validate(key);
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Key == key && x.Operation == operation,
                cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (record.ExpiresAt <= clock.UtcNow)
        {
            return null;
        }

        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyKeyReusedException(key);
        }

        return new IdempotencyReplay(record.StatusCode, record.ResponseBody);
    }

    public async Task StoreAsync(
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        int statusCode,
        string responseBody,
        CancellationToken cancellationToken = default)
    {
        IdempotencyKeyRules.Validate(key);
        var createdAt = clock.UtcNow;
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            insert into foundation.idempotency_records(
                id, tenant_id, key, operation, request_hash, status_code,
                response_body, created_at, expires_at)
            values (
                {Guid.NewGuid()}, {tenantId}, {key}, {operation}, {requestHash}, {statusCode},
                cast({responseBody} as jsonb), {createdAt}, {createdAt.AddDays(7)})
            on conflict (tenant_id, key, operation) do update set
                id = excluded.id,
                request_hash = excluded.request_hash,
                status_code = excluded.status_code,
                response_body = excluded.response_body,
                created_at = excluded.created_at,
                expires_at = excluded.expires_at
            where foundation.idempotency_records.expires_at <= excluded.created_at;
            """,
            cancellationToken);

        if (affected != 0)
        {
            return;
        }

        var existingHash = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .Where(record => record.TenantId == tenantId && record.Key == key && record.Operation == operation)
            .Select(record => record.RequestHash)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingHash is not null && !string.Equals(existingHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyKeyReusedException(key);
        }

        throw new IdempotencyOperationInProgressException();
    }
}
