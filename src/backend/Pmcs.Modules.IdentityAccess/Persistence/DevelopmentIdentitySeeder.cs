using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.IdentityAccess.Domain;

namespace Pmcs.Modules.IdentityAccess.Persistence;

internal sealed partial class DevelopmentIdentitySeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevelopmentIdentitySeeder> logger) : IHostedService
{
    public static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DemoProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DemoMembershipId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || !bool.TryParse(configuration["PMCS_SEED_ENABLED"], out var enabled) || !enabled)
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

        if (!await dbContext.Users.AnyAsync(x => x.Id == DemoUserId, cancellationToken))
        {
            dbContext.Users.Add(UserAccount.Create(
                DemoUserId,
                DemoTenantId,
                "مدیر پروژه نمونه",
                "demo@pmcs.local",
                TenantRole.TenantAdministrator,
                clock.UtcNow));
        }

        if (!await dbContext.ProjectMemberships.AnyAsync(x => x.Id == DemoMembershipId, cancellationToken))
        {
            dbContext.ProjectMemberships.Add(ProjectMembership.Assign(
                DemoMembershipId,
                DemoTenantId,
                DemoProjectId,
                DemoUserId,
                "ProjectManager",
                clock.UtcNow));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        LogSeedAvailable(logger, DemoTenantId, DemoUserId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Development identity seed is available for tenant {TenantId} and user {UserId}.")]
    private static partial void LogSeedAvailable(ILogger logger, Guid tenantId, Guid userId);
}
