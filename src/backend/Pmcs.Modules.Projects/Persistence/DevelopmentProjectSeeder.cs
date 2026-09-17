using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Testing;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Persistence;

internal sealed partial class DevelopmentProjectSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevelopmentProjectSeeder> logger) : IHostedService
{
    public static readonly Guid DemoProjectId = PmcsTestDataSet.ProjectId;
    private static readonly Guid DemoLocationId = PmcsTestDataSet.RootLocationId;
    private static readonly Guid DemoTenantId = PmcsTestDataSet.TenantId;
    private static readonly Guid DemoUserId = PmcsTestDataSet.QaSuperAdministrator.UserId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if ((!environment.IsDevelopment() && !environment.IsEnvironment("QA")) ||
            !bool.TryParse(configuration["PMCS_SEED_ENABLED"], out var enabled) ||
            !enabled)
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
