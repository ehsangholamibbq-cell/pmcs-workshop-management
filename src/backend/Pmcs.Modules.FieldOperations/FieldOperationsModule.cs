using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Endpoints;
using Pmcs.Modules.FieldOperations.Migrations;
using Pmcs.Modules.FieldOperations.Persistence;
using Pmcs.Modules.FieldOperations.Services;

namespace Pmcs.Modules.FieldOperations;

public sealed class FieldOperationsModule : IModule
{
    public string Name => "FieldOperations";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddDbContext<FieldOperationsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApprovedDailyFactSource, ApprovedDailyFactSource>();
        services.AddScoped<IDailyFactDirectory, DailyFactDirectory>();
        services.AddScoped<IProgressFactSource, ProgressFactSource>();
        services.AddScoped<IOfflineFieldOperationHandler, OfflineDailyReportOperationHandler>();
        services.AddSingleton<IDatabaseMigration, FieldOperationsInitialMigration>();
        services.AddSingleton<IDatabaseMigration, FieldOperationsStructuredFactsMigration>();
        services.AddSingleton<IDatabaseMigration, FieldOperationsReviewWorkflowMigration>();
        services.AddSingleton<IDatabaseMigration, FieldOperationsMeasurementLinkMigration>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDailyReportEndpoints();
    }
}
