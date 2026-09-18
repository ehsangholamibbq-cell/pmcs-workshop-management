using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.IdentityAccess.Endpoints;
using Pmcs.Modules.IdentityAccess.Migrations;
using Pmcs.Modules.IdentityAccess.Persistence;
using Pmcs.Modules.IdentityAccess.Services;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Contracts;

namespace Pmcs.Modules.IdentityAccess;

public sealed class IdentityAccessModule : IModule
{
    public string Name => "IdentityAccess";

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleManifestSchemas.VersionOne,
        "identity-access.core",
        "Identity Access and Member Profiles",
        "1.1.0",
        [
            "identity.login-experience",
            "identity.member-profiles",
            "identity.project-memberships"
        ],
        ["platform.foundation", "documents.shared"],
        "identity-access",
        "1.1.0",
        false,
        [
            new PermissionManifest(
                "identity.users.manage",
                PermissionScope.Tenant,
                ManifestRiskClass.Critical,
                "Manage tenant users, invitations and project memberships."),
            new PermissionManifest(
                "login-experience.manage",
                PermissionScope.Tenant,
                ManifestRiskClass.High,
                "Create, preview, publish and roll back allowlisted login presentations."),
            new PermissionManifest(
                "member-profile.read-self",
                PermissionScope.Tenant,
                ManifestRiskClass.Low,
                "Read the authenticated member's profile."),
            new PermissionManifest(
                "member-profile.update-self",
                PermissionScope.Tenant,
                ManifestRiskClass.Medium,
                "Update allowlisted fields and avatar association on the authenticated member's profile."),
            new PermissionManifest(
                "member-profile.avatar.publish-self",
                PermissionScope.Tenant,
                ManifestRiskClass.Medium,
                "Release the authenticated member's own clean and policy-constrained profile image."),
            new PermissionManifest(
                "member-profile.read-directory",
                PermissionScope.Project,
                ManifestRiskClass.Medium,
                "Read a member profile when the requester and member share an active project scope."),
            new PermissionManifest(
                "member-profile.manage-directory",
                PermissionScope.Tenant,
                ManifestRiskClass.High,
                "Manage organization-controlled member directory fields.")
        ],
        [
            new NavigationManifest(
                "identity.member-profile",
                "/profile",
                "پروفایل من",
                "member-profile.read-self",
                "identity.member-profiles",
                8_100),
            new NavigationManifest(
                "identity.login-experience",
                "/admin/login-experience",
                "ظاهر صفحه ورود",
                "login-experience.manage",
                "identity.login-experience",
                8_200)
        ],
        [],
        [
            new IntegrationEventManifest(
                "identity.member-profile.updated",
                1,
                IntegrationEventClassification.Confidential),
            new IntegrationEventManifest(
                "identity.login-experience.published",
                1,
                IntegrationEventClassification.Internal)
        ]);

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddDbContext<IdentityAccessDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectPermissionService, ProjectPermissionService>();
        services.AddScoped<IProjectMembershipBootstrapService, ProjectMembershipBootstrapService>();
        services.AddScoped<IActorAccessValidator, ActorAccessValidator>();
        services.AddScoped<IProjectAssigneeDirectory, ProjectAssigneeDirectory>();
        services.AddScoped<IProjectLeadershipDirectory, ProjectLeadershipDirectory>();
        services.AddScoped<IProjectPermissionRecipientDirectory, ProjectPermissionRecipientDirectory>();
        var provisioning = ReadProvisioningOptions(configuration);
        provisioning.Validate();
        services.AddSingleton(provisioning);
        services.AddHttpClient<IIdentityProviderAdministration, KeycloakIdentityProviderAdministration>(client =>
        {
            if (provisioning.Enabled)
            {
                client.BaseAddress = new Uri($"{provisioning.BaseUrl.TrimEnd('/')}/", UriKind.Absolute);
            }
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IDatabaseMigration, IdentityAccessInitialMigration>();
        services.AddSingleton<IDatabaseMigration, IdentityAdministrationMigration>();
        services.AddSingleton<IDatabaseMigration, IdentityAdministrationCleanupMigration>();
        services.AddSingleton<IDatabaseMigration, UserAccessEpochMigration>();
        services.AddSingleton<IDatabaseMigration, IdentityExperienceMigration>();
        services.AddHostedService<DevelopmentIdentitySeeder>();
        services.AddHostedService<IdentityAdministrationWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapIdentityEndpoints();

    private static IdentityProvisioningOptions ReadProvisioningOptions(IConfiguration configuration) => new()
    {
        Enabled = ReadBool(configuration, "IdentityProvisioning:Enabled", false),
        BaseUrl = configuration["IdentityProvisioning:BaseUrl"]?.Trim() ?? string.Empty,
        Realm = configuration["IdentityProvisioning:Realm"]?.Trim() ?? "pmcs",
        ClientId = configuration["IdentityProvisioning:ClientId"]?.Trim() ?? "pmcs-identity-admin",
        ClientSecret = configuration["IdentityProvisioning:ClientSecret"] ?? string.Empty,
        WebClientId = configuration["IdentityProvisioning:WebClientId"]?.Trim() ?? "pmcs-web",
        WebReturnUrl = configuration["IdentityProvisioning:WebReturnUrl"]?.Trim() ?? string.Empty,
        InvitationLifespanSeconds = ReadInt(configuration, "IdentityProvisioning:InvitationLifespanSeconds", 86_400),
        PollSeconds = ReadInt(configuration, "IdentityProvisioning:PollSeconds", 5),
        MaximumAttempts = ReadInt(configuration, "IdentityProvisioning:MaximumAttempts", 5)
    };

    private static bool ReadBool(IConfiguration configuration, string key, bool fallback) =>
        bool.TryParse(configuration[key], out var value) ? value : fallback;

    private static int ReadInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var value) ? value : fallback;
}
