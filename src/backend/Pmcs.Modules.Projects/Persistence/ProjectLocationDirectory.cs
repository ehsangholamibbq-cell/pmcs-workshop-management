using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Persistence;

internal sealed class ProjectLocationDirectory(ProjectsDbContext dbContext) : IProjectLocationDirectory
{
    public async Task<ProjectLocationReference?> FindAsync(
        Guid tenantId,
        Guid projectId,
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var location = await dbContext.ProjectLocations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == locationId,
                cancellationToken);
        return ToReference(location);
    }

    public async Task<ProjectLocationReference?> FindActiveAsync(
        Guid tenantId,
        Guid projectId,
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var location = await dbContext.ProjectLocations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId &&
                    item.ProjectId == projectId &&
                    item.Id == locationId &&
                    item.Status == ProjectLocationStatus.Active,
                cancellationToken);

        return ToReference(location);
    }

    private static ProjectLocationReference? ToReference(ProjectLocation? location) => location is null
        ? null
        : new ProjectLocationReference(
            location.Id,
            location.ProjectId,
            location.Code,
            location.Name,
            location.ParentLocationId);
}
