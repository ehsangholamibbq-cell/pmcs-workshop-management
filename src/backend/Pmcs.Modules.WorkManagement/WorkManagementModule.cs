using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.WorkManagement.Endpoints;
using Pmcs.Modules.WorkManagement.Migrations;
using Pmcs.Modules.WorkManagement.Persistence;
using Pmcs.Modules.WorkManagement.Services;

namespace Pmcs.Modules.WorkManagement;

public sealed class WorkManagementModule : IModule
{
    public string Name => "WorkManagement";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<WorkManagementDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ITransactionalNotificationWriter, TransactionalNotificationWriter>();
        services.AddSingleton<IDatabaseMigration, WorkManagementInitialMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints.MapWorkManagementEndpoints();
}
