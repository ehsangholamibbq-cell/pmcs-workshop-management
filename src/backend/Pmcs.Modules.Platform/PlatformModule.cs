using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.BuildingBlocks.Persistence;
using Pmcs.Modules.Platform.Endpoints;
using Pmcs.Modules.Platform.Migrations;
using Pmcs.Modules.Platform.Persistence;
using Pmcs.Modules.Platform.Services;

namespace Pmcs.Modules.Platform;

public sealed class PlatformModule : IModule
{
    public string Name => "Platform";

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleManifestSchemas.VersionOne,
        "platform.foundation",
        "PMCS Platform Foundation",
        "1.1.0",
        [
            "platform.module-catalog",
            "platform.navigation-manifest",
            "platform.agent-tool-manifest",
            "platform.integration-event-manifest"
        ],
        [],
        "platform",
        "1.1.0",
        false,
        [
            new PermissionManifest(
                "platform.modules.read",
                PermissionScope.Tenant,
                ManifestRiskClass.Low,
                "Read validated module, navigation, event and tool contracts."),
            new PermissionManifest(
                "platform.modules.manage",
                PermissionScope.Tenant,
                ManifestRiskClass.High,
                "Manage platform module activation and compatibility settings.")
        ],
        [
            new NavigationManifest(
                "platform.modules",
                "/admin/platform/modules",
                "ماژول‌های سامانه",
                "platform.modules.read",
                "platform.module-catalog",
                9_000)
        ],
        [
            new ToolManifest(
                "platform.modules.describe",
                "Returns the validated descriptor for a registered PMCS module.",
                """{"type":"object","properties":{"moduleId":{"type":"string"}},"required":["moduleId"],"additionalProperties":false}""",
                """{"type":"object","required":["schemaVersion","moduleId","version"],"additionalProperties":true}""",
                "platform.modules.read",
                ManifestRiskClass.Low,
                ToolAccessMode.ReadOnly)
        ],
        [
            new IntegrationEventManifest(
                "platform.module-catalog.snapshot",
                1,
                IntegrationEventClassification.Internal)
        ]);

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
        endpoints.MapPlatformModuleEndpoints();
        endpoints.MapGet("/api/v1/foundation", () => Results.Ok(new
        {
            service = "PMCS API",
            architecture = "Modular Monolith",
            status = "Foundation",
            utc = DateTimeOffset.UtcNow
        })).AllowAnonymous();
    }
}
