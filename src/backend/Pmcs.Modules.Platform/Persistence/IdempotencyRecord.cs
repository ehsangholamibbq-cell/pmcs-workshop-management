namespace Pmcs.Modules.Platform.Persistence;

internal sealed class IdempotencyRecord
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public required string Key { get; init; }

    public required string Operation { get; init; }

    public required string RequestHash { get; init; }

    public int StatusCode { get; init; }

    public required string ResponseBody { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }
}
