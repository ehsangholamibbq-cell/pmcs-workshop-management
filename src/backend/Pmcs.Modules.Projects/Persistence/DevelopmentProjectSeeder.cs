using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Persistence;

internal sealed partial class DevelopmentProjectSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevelopmentProjectSeeder> logger) : IHostedService
{
    public static readonly Guid DemoProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DemoLocationId = Guid.Parse("33333333-3333-4333-8333-333333333334");
    private static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DemoUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !bool.TryParse(configuration["PMCS_SEED_ENABLED"], out var enabled) || !enabled)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProjectsDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (await dbContext.Projects.AnyAsync(project => project.Id == DemoProjectId, cancellationToken))
        {
            return;
        }

        var project = Project.Create(
            DemoProjectId,
            DemoTenantId,
            "DEMO-01",
            "پروژه نمونه ساختمان اداری–تجاری",
            ContractModel.ConstructionManagement,
            PlanningMode.None,
            CapabilityMode.SetupRequired,
            CapabilityMode.NotEnabled,
            CapabilityMode.NotEnabled,
            "Asia/Tehran",
            DemoUserId,
            clock.UtcNow);
        project.Activate(project.Revision, DemoUserId, clock.UtcNow);

        var location = ProjectLocation.Create(
            DemoLocationId,
            DemoTenantId,
            DemoProjectId,
            "ROOT",
            "کل پروژه",
            null,
            DemoUserId,
            clock.UtcNow);

        dbContext.Projects.Add(project);
        dbContext.ProjectLocations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);
        LogSeedCreated(logger, DemoProjectId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Development project seed {ProjectId} created without WBS or budget baseline and with HSE disabled.")]
    private static partial void LogSeedCreated(ILogger logger, Guid projectId);
}
