using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Persistence;

namespace Pmcs.Modules.Intelligence.Services;

internal sealed class IntelligenceAdministrationAccess(
    IntelligenceDbContext dbContext,
    IProjectPermissionService identity,
    IClock clock)
{
    internal async Task<bool> CanManageProvidersAsync(ICurrentActor actor, CancellationToken cancellationToken) =>
        await HasGrantAsync(actor, null, IntelligenceAdministrationPermissions.ProvidersManage, cancellationToken);

    internal async Task<bool> CanPublishProfilesAsync(ICurrentActor actor, CancellationToken cancellationToken) =>
        await HasGrantAsync(actor, null, IntelligenceAdministrationPermissions.ProfilesPublish, cancellationToken);

    internal async Task<bool> CanSelectProfileAsync(
        ICurrentActor actor, Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || tenantId != actor.TenantId)
        {
            return false;
        }

        // Delegation is restricted to a current organization administrator; the role alone grants nothing.
        return await identity.HasTenantPermissionAsync(
                actor.TenantId, actor.UserId, "identity.users.manage", cancellationToken) &&
            await HasGrantAsync(actor, tenantId, IntelligenceAdministrationPermissions.ProfilesSelect, cancellationToken);
    }

    internal async Task<bool> CanReadCatalogAsync(
        ICurrentActor actor, Guid? tenantId, CancellationToken cancellationToken) =>
        (tenantId is null || tenantId == actor.TenantId) &&
        (await HasGrantAsync(actor, tenantId, IntelligenceAdministrationPermissions.CatalogRead, cancellationToken) ||
         tenantId == actor.TenantId &&
         await HasGrantAsync(actor, null, IntelligenceAdministrationPermissions.CatalogRead, cancellationToken));

    private async Task<bool> HasGrantAsync(
        ICurrentActor actor, Guid? tenantId, string permission, CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated || actor.TenantId == Guid.Empty || actor.UserId == Guid.Empty ||
            !IntelligenceAdministrationPermissions.IsValidScope(permission, tenantId) ||
            !await identity.HasTenantPermissionAsync(
                actor.TenantId, actor.UserId, "member-profile.read-self", cancellationToken))
        {
            return false;
        }

        var now = clock.UtcNow;
        return await dbContext.AdministrationGrants.AsNoTracking().AnyAsync(grant =>
            grant.ActorTenantId == actor.TenantId && grant.ActorId == actor.UserId &&
            grant.ScopeTenantId == tenantId && grant.Permission == permission &&
            grant.RevokedAt == null && grant.StartsAt <= now &&
            (grant.ExpiresAt == null || grant.ExpiresAt > now), cancellationToken);
    }
}
