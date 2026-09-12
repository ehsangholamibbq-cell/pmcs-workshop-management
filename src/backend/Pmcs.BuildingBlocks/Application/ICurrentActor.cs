namespace Pmcs.BuildingBlocks.Application;

public interface ICurrentActor
{
    bool IsAuthenticated { get; }

    Guid TenantId { get; }

    Guid UserId { get; }

    DateTimeOffset? TokenIssuedAt { get; }

    string? DeviceId { get; }
}
