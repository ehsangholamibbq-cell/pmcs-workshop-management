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

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddDbContext<IdentityAccessDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectPermissionService, ProjectPermissionService>();
        services.AddScoped<IActorAccessValidator, ActorAccessValidator>();
        services.AddScoped<IProjectAssigneeDirectory, ProjectAssigneeDirectory>();
        services.AddScoped<IProjectLeadershipDirectory, ProjectLeadershipDirectory>();
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
