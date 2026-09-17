using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Platform.Migrations;
using Pmcs.Modules.Platform.Persistence;
using Pmcs.Modules.Platform.Services;

namespace Pmcs.Modules.Platform;

public sealed class PlatformModule : IModule
{
    public string Name => "Platform";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContext<PlatformDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<ITransactionalSideEffectWriter, TransactionalSideEffectWriter>();
        services.AddSingleton<IDatabaseMigration, PlatformInitialMigration>();
        services.AddSingleton<IDatabaseMigration, PlatformIdempotencyRetentionMigration>();
        services.AddSingleton<IDatabaseMigration, PlatformMigrationLedgerNormalizationMigration>();
        services.AddHostedService<DatabaseMigrationRunner>();
        services.AddHostedService<IdempotencyRetentionWorker>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgres", tags: ["ready"]);
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/foundation", () => Results.Ok(new
        {
            service = "PMCS API",
            architecture = "Modular Monolith",
            status = "Foundation",
            utc = DateTimeOffset.UtcNow
        })).AllowAnonymous();
    }
}
