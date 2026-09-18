using Pmcs.BuildingBlocks.Domain;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.IdentityAccess;
using Pmcs.Modules.IdentityAccess.Domain;
using Pmcs.Modules.IdentityAccess.Services;

namespace Pmcs.Domain.Tests;

public sealed class IdentityExperienceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MemberProfileKeepsDirectoryFieldOutOfSelfService()
    {
        var userId = Guid.NewGuid();
        var profile = MemberProfile.Create(userId, Guid.NewGuid(), Now);
        profile.UpdateDirectory(
            1,
            "سرپرست کارگاه",
            "عملیات",
            "+98 21 1234",
            null,
            null,
            Guid.NewGuid(),
            Now.AddMinutes(1));

        profile.UpdateSelf(
            2,
            "مدیر کارگاه",
            "+98 21 5678",
            null,
            null,
            userId,
            Now.AddMinutes(2));

        Assert.Equal("عملیات", profile.OrganizationUnit);
        Assert.Equal("مدیر کارگاه", profile.JobTitle);
        Assert.Equal(3, profile.Revision);
    }

    [Fact]
    public void AvatarRequiresReleasedDocumentIdentityAndNormalizedCrop()
    {
        var profile = MemberProfile.Create(Guid.NewGuid(), Guid.NewGuid(), Now);

        var exception = Assert.Throws<DomainRuleException>(() => profile.UpdateSelf(
            1,
            null,
            null,
            Guid.NewGuid(),
            new ProfileAvatarCrop(0.8m, 0.8m, 0.4m, 0.4m),
            Guid.NewGuid(),
            Now));

        Assert.Equal("member-profile.avatar-crop.invalid", exception.Code);
    }

    [Fact]
    public void LoginExperienceOnlyPublishesAllowlistedPlainTextDescriptor()
    {
        var experience = CreateLoginExperience();

        experience.Publish(1, Guid.NewGuid(), Now.AddMinutes(1));
        experience.MarkSuperseded(Guid.NewGuid(), Now.AddMinutes(2));
        experience.Publish(3, Guid.NewGuid(), Now.AddMinutes(3));

        Assert.Equal(LoginExperienceStatus.Published, experience.Status);
        Assert.Equal(4, experience.Revision);
        Assert.Equal(
            "login-experience.headline.invalid",
            Assert.Throws<DomainRuleException>(() => CreateLoginExperience("<script>bad</script>")).Code);
    }

    [Fact]
    public void ProfilePermissionsAreCentralAndFailClosed()
    {
        Assert.True(ProjectPermissionService.GrantsTenant(
            TenantRole.Member,
            "member-profile.read-self"));
        Assert.True(ProjectPermissionService.GrantsRole(
            "Observer",
            "member-profile.read-directory"));
        Assert.True(ProjectPermissionService.GrantsTenant(
            TenantRole.Member,
            "member-profile.avatar.publish-self"));
        Assert.False(ProjectPermissionService.GrantsTenant(
            TenantRole.Member,
            "member-profile.manage-directory"));
        Assert.False(ProjectPermissionService.GrantsRole(
            "UnknownRole",
            "member-profile.read-directory"));
    }

    [Fact]
    public void IdentityManifestDeclaresLoginProfileAndPrivacyContracts()
    {
        var descriptor = new IdentityAccessModule().Descriptor;

        Assert.Equal(ModuleManifestSchemas.VersionOne, descriptor.SchemaVersion);
        Assert.Equal("identity-access.core", descriptor.ModuleId);
        Assert.Contains("documents.shared", descriptor.Dependencies);
        Assert.Contains(descriptor.Permissions, item =>
            item.Key == "login-experience.manage" && item.RiskClass == ManifestRiskClass.High);
        Assert.Contains(descriptor.Permissions, item =>
            item.Key == "member-profile.update-self" && item.Scope == PermissionScope.Tenant);
        Assert.Contains(descriptor.Events, item =>
            item.Name == "identity.member-profile.updated" &&
            item.Classification == IntegrationEventClassification.Confidential);
    }

    private static LoginExperience CreateLoginExperience(string headline = "ساختن، فراتر از امروز") =>
        LoginExperience.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            LoginCompositionVariant.BlueprintSplit,
            LoginSurfaceTone.WarmStone,
            LoginAccentPalette.CorporateNavyGreen,
            LoginMotionPolicy.Balanced,
            "سامانه جامع مدیریت پروژه",
            headline,
            "مرکز فرمان یکپارچه و قابل ردیابی پروژه.",
            null,
            null,
            Guid.NewGuid(),
            Now);
}
