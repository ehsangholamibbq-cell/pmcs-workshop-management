using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.IdentityAccess.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ProjectMembershipTests
{
    [Fact]
    public void MembershipScopesOneAccountToOneProjectRole()
    {
        var membership = ProjectMembership.Assign(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SiteSupervisor",
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("SiteSupervisor", membership.RoleCode);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public void EmptyRoleIsRejected()
    {
        var exception = Assert.Throws<DomainRuleException>(() => ProjectMembership.Assign(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            " ",
            new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal("membership.role.invalid", exception.Code);
    }
}
