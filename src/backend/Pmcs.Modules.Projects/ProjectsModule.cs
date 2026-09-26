using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Endpoints;
using Pmcs.Modules.Projects.Migrations;
using Pmcs.Modules.Projects.Persistence;
using Pmcs.Modules.Projects.Services;

namespace Pmcs.Modules.Projects;

public sealed class ProjectsModule : IModule
{
    public string Name => "Projects";

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleManifestSchemas.VersionOne,
        "projects.core",
        "Projects and Controlled Bootstrap",
        "1.1.0",
        [
            "projects.registry",
            "projects.setup",
            "projects.locations",
            "projects.controlled-bootstrap"
        ],
        ["platform.foundation", "identity-access.core"],
        "projects",
        "1.1.0",
        false,
        [
            new PermissionManifest("projects.read", PermissionScope.Project, ManifestRiskClass.Low,
                "Read an authorized project and its setup state."),
            new PermissionManifest("projects.create", PermissionScope.Tenant, ManifestRiskClass.High,
                "Create a new independent draft project."),
            new PermissionManifest("projects.activate", PermissionScope.Project, ManifestRiskClass.High,
                "Activate a draft project after readiness validation."),
            new PermissionManifest("projects.locations.manage", PermissionScope.Project, ManifestRiskClass.High,
                "Create and retire project location structure entries."),
            new PermissionManifest("projects.calendar.configure", PermissionScope.Project, ManifestRiskClass.High,
                "Configure the project working calendar."),
            new PermissionManifest("projects.setup.configure", PermissionScope.Project, ManifestRiskClass.High,
                "Configure draft project setup values."),
            new PermissionManifest("projects.setup.configure-sensitive", PermissionScope.Project, ManifestRiskClass.Critical,
                "Change sensitive setup values after project activation."),
            new PermissionManifest("projects.planning.configure", PermissionScope.Project, ManifestRiskClass.High,
                "Change the project planning mode without rewriting facts."),
            new PermissionManifest("projects.bootstrap.preview", PermissionScope.Project, ManifestRiskClass.Medium,
                "Preview an allowlisted project bootstrap dry run."),
            new PermissionManifest("projects.bootstrap.create", PermissionScope.Tenant, ManifestRiskClass.High,
                "Create a destination draft and execute allowlisted setup contributors."),
            new PermissionManifest("projects.bootstrap.members_copy", PermissionScope.Project, ManifestRiskClass.High,
                "Create selected destination memberships that reference existing tenant users."),
            new PermissionManifest("projects.bootstrap.activate", PermissionScope.Project, ManifestRiskClass.High,
                "Activate a validated project bootstrap destination independently of execution.")
        ],
        [
            new NavigationManifest(
                "projects.bootstrap",
                "/project-bootstraps",
                "ساخت از روی پروژهٔ موجود",
                "projects.bootstrap.preview",
                "projects.controlled-bootstrap",
                1_200)
        ],
        [],
        [
            new IntegrationEventManifest(
                "projects.bootstrap.completed",
                1,
                IntegrationEventClassification.Confidential)
        ]);

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddDbContext<ProjectsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectTenantDirectory, ProjectTenantDirectory>();
        services.AddScoped<IProjectDirectory, ProjectDirectory>();
        services.AddScoped<IProjectLocationDirectory, ProjectLocationDirectory>();
        services.AddScoped<ProjectBootstrapPreviewFactory>();
        services.AddScoped<ProjectReadinessEvaluator>();
        services.AddSingleton<IDatabaseMigration, ProjectsInitialMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectCalendarAndFinanceMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectProcurementMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectLocationMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectActivationMetadataMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectSetupReadinessMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectBootstrapMigration>();
        services.AddHostedService<DevelopmentProjectSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapProjectEndpoints();
        endpoints.MapProjectLocationEndpoints();
        endpoints.MapProjectBootstrapEndpoints();
    }
}
