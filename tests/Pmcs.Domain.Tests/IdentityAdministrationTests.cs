using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Services;

namespace Pmcs.Domain.Tests;

public sealed class IdentityAdministrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InvitationIsDurableAndRequiresProvisioningBeforeItIsSent()
    {
        var invitation = UserInvitation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "کاربر نمونه",
            "USER@example.com",
            TenantRole.Member,
            Guid.NewGuid(),
            Now,
            Now.AddDays(1));

        Assert.Equal(UserInvitationStatus.Queued, invitation.Status);
        Assert.Equal("user@example.com", invitation.Email);
        Assert.Null(invitation.UserId);
        Assert.Null(invitation.ProviderUserId);

        invitation.BeginAttempt(Now.AddMinutes(1));
        Assert.Equal(UserInvitationStatus.Processing, invitation.Status);
        Assert.Equal(1, invitation.Attempts);

        var userId = Guid.NewGuid();
        invitation.BindProviderUser(userId);
        invitation.MarkSent(userId, Now.AddMinutes(2));
        Assert.Equal(UserInvitationStatus.Sent, invitation.Status);
        Assert.Equal(userId, invitation.UserId);
        Assert.Equal(userId, invitation.ProviderUserId);
    }

    [Fact]
    public void ExpiredInvitationDoesNotBeginProvisioning()
    {
        var invitation = UserInvitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Guid.NewGuid(), Now, Now.AddHours(1));

        invitation.BeginAttempt(Now.AddHours(1));

        Assert.Equal(UserInvitationStatus.Expired, invitation.Status);
        Assert.Equal(0, invitation.Attempts);
    }

    [Fact]
    public void RevokedInvitationCannotBeRequeued()
    {
        var invitation = UserInvitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Guid.NewGuid(), Now, Now.AddHours(1));
        invitation.Revoke();

        var exception = Assert.Throws<DomainRuleException>(() => invitation.Requeue(Now, Now.AddHours(1)));
        Assert.Equal("invitation.status.invalid", exception.Code);
    }

    [Fact]
    public void DeactivatedAccountCannotBeReactivated()
    {
        var account = UserAccount.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Now);
        account.Deactivate(Now.AddMinutes(1));

        var exception = Assert.Throws<DomainRuleException>(() => account.Reactivate(Now.AddMinutes(2)));
        Assert.Equal("user.status.terminal", exception.Code);
    }

    [Fact]
    public void AccountStatusTransitionAdvancesAccessEpoch()
    {
        var account = UserAccount.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Now);

        account.Suspend(Now.AddMinutes(1));
        account.Reactivate(Now.AddMinutes(2));

        Assert.Equal(UserAccountStatus.Active, account.Status);
        Assert.Equal(Now.AddMinutes(2), account.AccessValidAfter);
    }

    [Fact]
    public void NewAccountAcceptsATokenIssuedInItsCreationSecond()
    {
        var account = UserAccount.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Now.AddMilliseconds(500));

        Assert.True(ActorAccessValidator.IsTokenCurrent(
            DateTimeOffset.FromUnixTimeSeconds(Now.ToUnixTimeSeconds()),
            account.AccessValidAfter));
    }

    [Fact]
    public void UnsupportedProjectRoleIsRejected()
    {
        var exception = Assert.Throws<DomainRuleException>(() => ProjectMembership.Assign(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "UnknownRole", Now));

        Assert.Equal("membership.role.invalid", exception.Code);
    }

    [Theory]
    [InlineData("QualityController")]
    [InlineData("HseOfficer")]
    public void IndependentQualityAndHseRolesAreSupported(string roleCode)
    {
        Assert.True(ProjectRoleCatalog.IsSupported(roleCode));
    }

    [Fact]
    public void ProviderOperationRetriesWithoutChangingItsIntent()
    {
        var operation = IdentityProviderOperation.Queue(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            IdentityProviderOperationType.DisableAndLogout, Now);

        operation.BeginAttempt();
        operation.ScheduleRetry("identity_provider.unavailable", Now.AddMinutes(2));

        Assert.Equal(IdentityProviderOperationStatus.RetryScheduled, operation.Status);
        Assert.Equal(IdentityProviderOperationType.DisableAndLogout, operation.OperationType);
        Assert.Equal(1, operation.Attempts);
    }

    [Fact]
    public void InvitationCannotSwitchToAnotherProviderIdentity()
    {
        var invitation = UserInvitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Guid.NewGuid(), Now, Now.AddHours(1));
        invitation.BindProviderUser(Guid.NewGuid());

        var exception = Assert.Throws<DomainRuleException>(() => invitation.BindProviderUser(Guid.NewGuid()));

        Assert.Equal("invitation.provider_user.conflict", exception.Code);
    }

    [Fact]
    public void FailedInvitationCanReleaseAConfirmedDeletedProviderIdentity()
    {
        var providerUserId = Guid.NewGuid();
        var invitation = UserInvitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Guid.NewGuid(), Now, Now.AddHours(1));
        invitation.BeginAttempt(Now.AddMinutes(1));
        invitation.BindProviderUser(providerUserId);
        invitation.Fail("identity_provider.invitation.delivery_failed");

        invitation.ReleaseDeletedProviderUser(providerUserId);
        invitation.Requeue(Now.AddMinutes(2), Now.AddDays(1));
        var replacementId = Guid.NewGuid();
        invitation.BindProviderUser(replacementId);

        Assert.Equal(replacementId, invitation.ProviderUserId);
        Assert.Equal(UserInvitationStatus.Queued, invitation.Status);
    }

    [Fact]
    public void ActiveInvitationCannotReleaseItsProviderIdentity()
    {
        var providerUserId = Guid.NewGuid();
        var invitation = UserInvitation.Create(
            Guid.NewGuid(), Guid.NewGuid(), "کاربر نمونه", "user@example.com",
            TenantRole.Member, Guid.NewGuid(), Now, Now.AddHours(1));
        invitation.BindProviderUser(providerUserId);

        var exception = Assert.Throws<DomainRuleException>(() =>
            invitation.ReleaseDeletedProviderUser(providerUserId));

        Assert.Equal("invitation.provider_user.release_denied", exception.Code);
    }
}
