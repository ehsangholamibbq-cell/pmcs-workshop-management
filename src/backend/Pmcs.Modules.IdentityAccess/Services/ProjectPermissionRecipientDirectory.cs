using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ProjectPermissionRecipientDirectory(
    IdentityAccessDbContext dbContext,
    IClock clock) : IProjectPermissionRecipientDirectory
{
    public async Task<IReadOnlyCollection<ProjectPermissionRecipient>> ListAsync(
        Guid tenantId,
        Guid projectId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || string.IsNullOrWhiteSpace(permission))
        {
            return [];
        }

        var now = clock.UtcNow;
        var members = await (
            from membership in dbContext.ProjectMemberships.AsNoTracking()
            join user in dbContext.Users.AsNoTracking()
                on new { membership.TenantId, membership.UserId }
                equals new { user.TenantId, UserId = user.Id }
            where membership.TenantId == tenantId && membership.ProjectId == projectId &&
                membership.Status == MembershipStatus.Active && membership.StartsAt <= now &&
                (membership.EndsAt == null || membership.EndsAt > now) &&
                user.Status == UserAccountStatus.Active
            select new { user.Id, user.DisplayName, membership.RoleCode })
            .ToArrayAsync(cancellationToken);

        var administrators = await dbContext.Users.AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.Status == UserAccountStatus.Active &&
                user.TenantRole == TenantRole.TenantAdministrator)
            .Select(user => new ProjectPermissionRecipient(user.Id, user.DisplayName))
            .ToArrayAsync(cancellationToken);

        return members
            .Where(member => ProjectPermissionService.GrantsRole(member.RoleCode, permission))
            .Select(member => new ProjectPermissionRecipient(member.Id, member.DisplayName))
            .Concat(administrators)
            .GroupBy(recipient => recipient.UserId)
            .Select(group => group.First())
            .OrderBy(recipient => recipient.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
