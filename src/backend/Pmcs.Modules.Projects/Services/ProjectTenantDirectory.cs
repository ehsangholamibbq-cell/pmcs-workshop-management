using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Persistence;

namespace Pmcs.Modules.Projects.Services;

internal sealed class ProjectTenantDirectory(ProjectsDbContext dbContext) : IProjectTenantDirectory
{
    public async Task<IReadOnlySet<Guid>> ExistingProjectIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var requested = projectIds.Distinct().ToArray();
        return (await dbContext.Projects
                .AsNoTracking()
                .Where(project => project.TenantId == tenantId && requested.Contains(project.Id))
                .Select(project => project.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }
}
