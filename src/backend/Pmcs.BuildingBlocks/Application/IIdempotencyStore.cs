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

public sealed class IdempotencyKeyInvalidException()
    : Exception($"Idempotency-Key must contain between 1 and {IdempotencyKeyRules.MaxKeyLength} characters.");

public sealed class IdempotencyOperationInProgressException()
    : Exception("An operation with this idempotency key is already being committed; retry the request.");

public static class IdempotencyKeyRules
{
    public const int MaxKeyLength = 160;

    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            throw new IdempotencyKeyInvalidException();
        }
    }
}
