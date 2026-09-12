namespace Pmcs.BuildingBlocks.Application;

public interface IProjectTenantDirectory
{
    Task<IReadOnlySet<Guid>> ExistingProjectIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);
}
