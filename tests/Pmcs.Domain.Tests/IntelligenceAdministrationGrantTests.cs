using Pmcs.Modules.Intelligence.Domain;

namespace Pmcs.Domain.Tests;

public sealed class IntelligenceAdministrationGrantTests
{
    [Fact]
    public void OrdinaryRoleNeverCreatesImplicitAdministrationGrant()
    {
        Assert.False(IntelligenceAdministrationPermissions.IsValidScope(
            IntelligenceAdministrationPermissions.ProvidersManage, Guid.NewGuid()));
        Assert.False(IntelligenceAdministrationPermissions.IsValidScope(
            IntelligenceAdministrationPermissions.ProfilesSelect, null));
        Assert.False(IntelligenceAdministrationPermissions.IsValidScope("admin", null));
    }

    [Fact]
    public void DelegatedSelectionIsBoundToActorTenantScopeAndExpiry()
    {
        var actorTenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var grant = IntelligenceAdministrationGrant.Issue(
            Guid.NewGuid(), actorTenant, actor, actorTenant,
            IntelligenceAdministrationPermissions.ProfilesSelect, Guid.NewGuid(),
            now, now.AddHours(1));

        Assert.True(grant.Allows(actorTenant, actor, actorTenant,
            IntelligenceAdministrationPermissions.ProfilesSelect, now));
        Assert.False(grant.Allows(Guid.NewGuid(), actor, actorTenant,
            IntelligenceAdministrationPermissions.ProfilesSelect, now));
        Assert.False(grant.Allows(actorTenant, actor, Guid.NewGuid(),
            IntelligenceAdministrationPermissions.ProfilesSelect, now));
        Assert.False(grant.Allows(actorTenant, actor, actorTenant,
            IntelligenceAdministrationPermissions.ProvidersManage, now));
        Assert.False(grant.Allows(actorTenant, actor, actorTenant,
            IntelligenceAdministrationPermissions.ProfilesSelect, now.AddHours(1)));

        grant.Revoke(now.AddMinutes(5));
        Assert.False(grant.Allows(actorTenant, actor, actorTenant,
            IntelligenceAdministrationPermissions.ProfilesSelect, now.AddMinutes(6)));
    }

    [Fact]
    public void GrantRejectsInvalidScopeOrLifetime()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => IntelligenceAdministrationGrant.Issue(
            id, id, id, id, IntelligenceAdministrationPermissions.ProvidersManage,
            id, now, null));
        Assert.Throws<ArgumentException>(() => IntelligenceAdministrationGrant.Issue(
            id, id, id, null, IntelligenceAdministrationPermissions.ProfilesPublish,
            id, now, now));
    }
}
