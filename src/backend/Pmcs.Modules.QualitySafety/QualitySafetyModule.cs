using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.QualitySafety.Endpoints;
using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.QualitySafety.Migrations;
using Pmcs.Modules.QualitySafety.Persistence;
using Pmcs.Modules.QualitySafety.Services;

namespace Pmcs.Modules.QualitySafety;

public sealed class QualitySafetyModule : IModule
{
    public string Name => "QualitySafety";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<QualitySafetyDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IProjectQualityHseReportingSource, ProjectQualityHseReportingSource>();
        services.AddSingleton<IDatabaseMigration, QualitySafetyInitialMigration>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapQualitySafetyEndpoints();
}
