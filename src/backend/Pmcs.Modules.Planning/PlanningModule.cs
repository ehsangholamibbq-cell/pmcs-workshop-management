using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Endpoints;
using Pmcs.Modules.Planning.Migrations;
using Pmcs.Modules.Planning.Persistence;
using Pmcs.Modules.Planning.Services;

namespace Pmcs.Modules.Planning;

public sealed class PlanningModule : IModule
{
    public string Name => "Planning";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<PlanningDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IMeasurementItemDirectory, MeasurementItemDirectory>();
        services.AddScoped<IProjectProgressReportingSource, ProjectProgressReportingSource>();
        services.AddSingleton<IDatabaseMigration, PlanningInitialMigration>();
        services.AddSingleton<IDatabaseMigration, PlanningBaselineMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapPlanningEndpoints();
}
