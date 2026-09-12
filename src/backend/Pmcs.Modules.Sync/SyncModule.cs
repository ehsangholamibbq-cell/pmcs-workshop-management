using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Sync.Endpoints;
using Pmcs.Modules.Sync.Migrations;
using Pmcs.Modules.Sync.Persistence;

namespace Pmcs.Modules.Sync;

public sealed class SyncModule : IModule
{
    public string Name => "Sync";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddDbContext<SyncDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IDatabaseMigration, SyncControlInitialMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapSyncGatewayEndpoints();
}
