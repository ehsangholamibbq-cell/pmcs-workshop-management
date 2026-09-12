namespace Pmcs.BuildingBlocks.Application;

public sealed record ProjectPermissionScope(
    bool AllProjects,
    IReadOnlySet<Guid> ProjectIds);

public interface IProjectPermissionService
{
    Task<bool> HasTenantPermissionAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default);

    Task<bool> HasProjectPermissionAsync(
        Guid tenantId,
        Guid userId,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken = default);

    Task<ProjectPermissionScope> GetProjectScopeAsync(
        Guid tenantId,
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default);
}
