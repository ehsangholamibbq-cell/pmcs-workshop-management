using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.QualitySafety.Endpoints;
using Pmcs.Modules.QualitySafety.Migrations;
using Pmcs.Modules.QualitySafety.Persistence;

namespace Pmcs.Modules.QualitySafety;

public sealed class QualitySafetyModule : IModule
{
    public string Name => "QualitySafety";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<QualitySafetyDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IDatabaseMigration, QualitySafetyInitialMigration>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapQualitySafetyEndpoints();
}
