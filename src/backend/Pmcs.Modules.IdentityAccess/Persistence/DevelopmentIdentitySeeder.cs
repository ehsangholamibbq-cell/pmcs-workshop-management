using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Testing;
using Pmcs.Modules.IdentityAccess.Domain;

namespace Pmcs.Modules.IdentityAccess.Persistence;

internal sealed partial class DevelopmentIdentitySeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevelopmentIdentitySeeder> logger) : IHostedService
{
    public static readonly Guid DemoTenantId = PmcsTestDataSet.TenantId;
    public static readonly Guid DemoUserId = PmcsTestDataSet.QaSuperAdministrator.UserId;
    public static readonly Guid DemoFieldUserId = PmcsTestDataSet.Actors[1].UserId;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if ((!environment.IsDevelopment() && !environment.IsEnvironment("QA")) ||
            !bool.TryParse(configuration["PMCS_SEED_ENABLED"], out var enabled) ||
            !enabled)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (!await dbContext.Tenants.AnyAsync(x => x.Id == DemoTenantId, cancellationToken))
        {
            dbContext.Tenants.Add(Tenant.Create(DemoTenantId, "سازمان نمونه PMCS", clock.UtcNow));
        }

        var qaEnabled = bool.TryParse(configuration["PMCS_QA_GATEWAY_ENABLED"], out var qaConfigured) &&
            qaConfigured;
        IEnumerable<PmcsTestActor> actors = qaEnabled
            ? PmcsTestDataSet.Actors
            : PmcsTestDataSet.Actors.Take(2);
        var actorCount = 0;
        foreach (var actor in actors)
        {
            actorCount++;
            if (!await dbContext.Users.AnyAsync(x => x.Id == actor.UserId, cancellationToken))
            {
                dbContext.Users.Add(UserAccount.Create(
                    actor.UserId,
                    DemoTenantId,
                    actor.DisplayName,
                    actor.Email,
                    Enum.Parse<TenantRole>(actor.TenantRole, ignoreCase: false),
                    clock.UtcNow));
            }

            if (!await dbContext.ProjectMemberships.AnyAsync(
                    x => x.Id == actor.MembershipId,
                    cancellationToken))
            {
                dbContext.ProjectMemberships.Add(ProjectMembership.Assign(
                    actor.MembershipId,
                    DemoTenantId,
                    PmcsTestDataSet.ProjectId,
                    actor.UserId,
                    actor.ProjectRole,
                    clock.UtcNow));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        LogSeedAvailable(logger, DemoTenantId, DemoUserId, actorCount);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Test identity seed is available for tenant {TenantId}, administrator {UserId} and {ActorCount} actors.")]
    private static partial void LogSeedAvailable(
        ILogger logger,
        Guid tenantId,
        Guid userId,
        int actorCount);
}
