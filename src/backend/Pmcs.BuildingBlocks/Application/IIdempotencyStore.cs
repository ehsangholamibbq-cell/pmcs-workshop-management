namespace Pmcs.BuildingBlocks.Application;

public sealed record IdempotencyReplay(int StatusCode, string ResponseBody);

public interface IIdempotencyStore
{
    Task<IdempotencyReplay?> FindAsync(
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        CancellationToken cancellationToken = default);

    Task StoreAsync(
        Guid tenantId,
        string key,
        string operation,
        string requestHash,
        int statusCode,
        string responseBody,
        CancellationToken cancellationToken = default);
}

public sealed class IdempotencyKeyReusedException(string key)
    : Exception($"Idempotency key '{key}' was already used with a different request.");
