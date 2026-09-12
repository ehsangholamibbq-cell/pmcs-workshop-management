using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.TechnicalOffice.Endpoints;
using Pmcs.Modules.TechnicalOffice.Migrations;
using Pmcs.Modules.TechnicalOffice.Persistence;

namespace Pmcs.Modules.TechnicalOffice;

public sealed class TechnicalOfficeModule : IModule
{
    public string Name => "TechnicalOffice";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<TechnicalOfficeDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IDatabaseMigration, TechnicalOfficeInitialMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapTechnicalOfficeEndpoints();
}
