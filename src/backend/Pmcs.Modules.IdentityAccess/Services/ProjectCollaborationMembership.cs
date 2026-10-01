using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Persistence;

namespace Pmcs.Modules.IdentityAccess.Services;

internal sealed class ProjectCollaborationMembership(
    IdentityAccessDbContext dbContext, IClock clock) : IProjectCollaborationMembership
{
    public Task<bool> IsActiveAsync(
        Guid tenantId, Guid projectId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || userId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        var now = clock.UtcNow;
        return (from membership in dbContext.ProjectMemberships.AsNoTracking()
                join user in dbContext.Users.AsNoTracking()
                    on new { membership.TenantId, membership.UserId }
                    equals new { user.TenantId, UserId = user.Id }
                join tenant in dbContext.Tenants.AsNoTracking()
                    on membership.TenantId equals tenant.Id
                where membership.TenantId == tenantId &&
                    membership.ProjectId == projectId &&
                    membership.UserId == userId &&
                    membership.Status == MembershipStatus.Active &&
                    membership.StartsAt <= now &&
                    (membership.EndsAt == null || membership.EndsAt > now) &&
                    user.Status == UserAccountStatus.Active &&
                    tenant.Status == TenantStatus.Active
                select membership.Id).AnyAsync(cancellationToken);
    }
}
