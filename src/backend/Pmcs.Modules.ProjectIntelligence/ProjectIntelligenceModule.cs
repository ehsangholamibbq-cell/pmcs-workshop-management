using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.ProjectIntelligence.Endpoints;
using Pmcs.Modules.ProjectIntelligence.Migrations;
using Pmcs.Modules.ProjectIntelligence.Persistence;
using Pmcs.Modules.ProjectIntelligence.Services;
using Pmcs.Modules.ProjectIntelligence.Contracts;

namespace Pmcs.Modules.ProjectIntelligence;

public sealed class ProjectIntelligenceModule : IModule
{
    public string Name => "ProjectIntelligence";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddDbContext<ProjectIntelligenceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectStateContextSource, ProjectStateContextSource>();
        services.AddSingleton<IDatabaseMigration, ProjectIntelligenceInitialMigration>();
        services.AddSingleton<IDatabaseMigration, ProjectStateV2Migration>();
        services.AddHostedService<ProjectStateRefreshWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapProjectIntelligenceEndpoints();
}
