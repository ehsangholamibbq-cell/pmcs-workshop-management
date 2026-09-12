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
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Key == key && x.Operation == operation,
                cancellationToken);

        if (record is null)
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
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = key,
            Operation = operation,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = clock.UtcNow,
            ExpiresAt = clock.UtcNow.AddDays(7)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
