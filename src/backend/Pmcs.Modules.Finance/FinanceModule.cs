using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Endpoints;
using Pmcs.Modules.Finance.Migrations;
using Pmcs.Modules.Finance.Persistence;
using Pmcs.Modules.Finance.Services;

namespace Pmcs.Modules.Finance;

public sealed class FinanceModule : IModule
{
    public string Name => "Finance";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        services.AddDbContext<FinanceDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IFinancialStateSource, FinancialStateSource>();
        services.AddScoped<IFinanceControlReadService, FinanceControlReadService>();
        services.AddScoped<IProjectFinancialPositionReportingSource, ProjectFinancialPositionReportingSource>();
        services.AddScoped<IFinanceVerificationService, FinanceVerificationService>();
        services.AddSingleton<IDatabaseMigration, FinanceInitialMigration>();
        services.AddSingleton<IDatabaseMigration, FinanceCommercialLinkMigration>();
        services.AddSingleton<IDatabaseMigration, FinanceControlAndLocationMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFinanceEndpoints();
        endpoints.MapFinanceControlEndpoints();
    }
}
