using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.ActionControl.Endpoints;
using Pmcs.Modules.ActionControl.Migrations;
using Pmcs.Modules.ActionControl.Persistence;
using Pmcs.Modules.ActionControl.Services;

namespace Pmcs.Modules.ActionControl;

public sealed class ActionControlModule : IModule
{
    public string Name => "ActionControl";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<ActionControlDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAttentionDispositionSource, AttentionDispositionSource>();
        services.AddScoped<IPortfolioActionSource, PortfolioActionSource>();
        services.AddHostedService<GovernanceDeadlineWorker>();
        services.AddSingleton<IDatabaseMigration, ActionControlInitialMigration>();
        services.AddSingleton<IDatabaseMigration, ActionGovernanceMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapActionControlEndpoints();
}
