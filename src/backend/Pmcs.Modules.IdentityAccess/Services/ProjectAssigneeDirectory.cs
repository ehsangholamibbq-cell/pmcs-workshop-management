using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ProjectAssigneeDirectory(
    IdentityAccessDbContext dbContext,
    Pmcs.BuildingBlocks.Application.IClock clock) : IProjectAssigneeDirectory
{
    public async Task<IReadOnlyCollection<ProjectAssignee>> ListAssignableAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var projectMembers = await (
            from membership in dbContext.ProjectMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.TenantId == tenantId && membership.ProjectId == projectId &&
                membership.Status == MembershipStatus.Active && membership.StartsAt <= now &&
                (membership.EndsAt == null || membership.EndsAt > now) &&
                user.TenantId == tenantId && user.Status == UserAccountStatus.Active
            select new { user.Id, user.DisplayName, membership.RoleCode })
            .ToListAsync(cancellationToken);
        var administrators = await dbContext.Users.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Status == UserAccountStatus.Active &&
                item.TenantRole == TenantRole.TenantAdministrator)
            .Select(item => new { item.Id, item.DisplayName })
            .ToListAsync(cancellationToken);

        return projectMembers
            .Select(item => new ProjectAssignee(item.Id, item.DisplayName,
                ProjectPermissionService.GrantsRole(item.RoleCode, "decisions.decide")))
            .Concat(administrators.Select(item => new ProjectAssignee(item.Id, item.DisplayName, true)))
            .GroupBy(item => item.UserId)
            .Select(group => new ProjectAssignee(group.Key, group.First().DisplayName,
                group.Any(item => item.CanDecide)))
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<ProjectAssignee?> FindAssignableAsync(
        Guid tenantId,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == userId && item.Status == UserAccountStatus.Active,
            cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (user.TenantRole == TenantRole.TenantAdministrator)
        {
            return new ProjectAssignee(user.Id, user.DisplayName, true);
        }

        var roles = await dbContext.ProjectMemberships.AsNoTracking().Where(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.UserId == userId &&
                item.Status == MembershipStatus.Active && item.StartsAt <= clock.UtcNow &&
                (item.EndsAt == null || item.EndsAt > clock.UtcNow))
            .Select(item => item.RoleCode)
            .ToListAsync(cancellationToken);
        return roles.Count > 0
            ? new ProjectAssignee(user.Id, user.DisplayName,
                roles.Any(role => ProjectPermissionService.GrantsRole(role, "decisions.decide")))
            : null;
    }
}
