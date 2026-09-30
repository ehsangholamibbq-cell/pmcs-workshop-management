using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.Intelligence.Persistence;

namespace Pmcs.Modules.Intelligence.Services;

internal sealed partial class ReferenceRunRecoveryWorker(
    IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<ReferenceRunRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bool.TryParse(configuration["Intelligence:INT1ReferenceEnabled"], out var enabled) ||
            !enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogRecoveryFailure(logger, exception);
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IntelligenceDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var writer = scope.ServiceProvider.GetRequiredService<ITransactionalSideEffectWriter>();
        // The maximum configured Run timeout is five minutes; allow another five for shutdown.
        var cutoff = clock.UtcNow.AddMinutes(-10);
        var abandoned = await db.ReferenceRuns
            .Where(item => item.RequestedAt < cutoff &&
                (item.Status == IntelligenceRunStatus.Requested ||
                 item.Status == IntelligenceRunStatus.Validated ||
                 item.Status == IntelligenceRunStatus.Running))
            .OrderBy(item => item.RequestedAt).Take(20).ToArrayAsync(cancellationToken);
        foreach (var run in abandoned)
        {
            run.Fail("ai.run.abandoned", clock.UtcNow);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await writer.WriteAuditAsync(db.Database.GetDbConnection(),
                transaction.GetDbTransaction(),
                new AuditEntry(run.TenantId, run.ProjectId, run.RequestedBy,
                    "IntelligenceReferenceRunAbandoned", "IntelligenceReferenceRun",
                    run.Id.ToString(), clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["status"] = run.Status.ToString(),
                        ["errorCode"] = run.ErrorCode,
                        ["profileVersionId"] = run.ProfileVersionId,
                        ["modelCatalogId"] = run.ModelCatalogId
                    }), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "INT1 reference Run recovery failed; retrying on next poll.")]
    private static partial void LogRecoveryFailure(ILogger logger, Exception exception);
}
