namespace Pmcs.BuildingBlocks.Application;

public interface IActorAccessValidator
{
    Task<bool> HasAccessAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset tokenIssuedAt,
        CancellationToken cancellationToken = default);
}
