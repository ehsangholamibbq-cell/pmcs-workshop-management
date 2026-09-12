using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ProjectLeadershipDirectory(
    IdentityAccessDbContext dbContext,
    IClock clock) : IProjectLeadershipDirectory
{
    public async Task<IReadOnlyCollection<ProjectLeaderRecord>> ListProjectManagersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
        {
            return [];
        }

        var ids = projectIds.Distinct().ToArray();
        return await (
            from membership in dbContext.ProjectMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on new { membership.TenantId, membership.UserId }
                equals new { user.TenantId, UserId = user.Id }
            where membership.TenantId == tenantId &&
                ids.Contains(membership.ProjectId) &&
                membership.RoleCode == "ProjectManager" &&
                membership.Status == MembershipStatus.Active &&
                membership.StartsAt <= clock.UtcNow &&
                (membership.EndsAt == null || membership.EndsAt > clock.UtcNow) &&
                user.Status == UserAccountStatus.Active
            orderby membership.ProjectId, user.DisplayName
            select new ProjectLeaderRecord(membership.ProjectId, user.Id, user.DisplayName))
            .ToArrayAsync(cancellationToken);
    }
}
