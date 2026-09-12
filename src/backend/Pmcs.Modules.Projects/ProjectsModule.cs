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

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddDbContext<ProjectsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectTenantDirectory, ProjectTenantDirectory>();
        services.AddScoped<IProjectDirectory, ProjectDirectory>();
        services.AddSingleton<IDatabaseMigration, ProjectsInitialMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectCalendarAndFinanceMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectProcurementMigration>();
        services.AddHostedService<DevelopmentProjectSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapProjectEndpoints();
}
