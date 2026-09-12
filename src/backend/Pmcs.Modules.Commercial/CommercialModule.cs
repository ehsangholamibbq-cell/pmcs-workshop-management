using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Endpoints;
using Pmcs.Modules.Commercial.Migrations;
using Pmcs.Modules.Commercial.Persistence;
using Pmcs.Modules.Commercial.Services;

namespace Pmcs.Modules.Commercial;

public sealed class CommercialModule : IModule
{
    public string Name => "Commercial";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<CommercialDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ICommercialReferenceDirectory, CommercialReferenceDirectory>();
        services.AddScoped<ICommercialStateSource, CommercialStateSource>();
        services.AddScoped<CommercialStateFactory>();
        services.AddSingleton<IDatabaseMigration, CommercialInitialMigration>();
        services.AddSingleton<IDatabaseMigration, CommercialSupplyRealityMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapCommercialEndpoints();
}
