using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Platform.Persistence;

namespace Pmcs.Modules.Platform.Services;

internal sealed partial class IdempotencyRetentionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<IdempotencyRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                var deleted = await dbContext.IdempotencyRecords
                    .Where(record => record.ExpiresAt <= clock.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0)
                {
                    LogExpiredReceiptsDeleted(logger, deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogCleanupFailed(logger, exception);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Deleted {Count} expired idempotency receipts.")]
    private static partial void LogExpiredReceiptsDeleted(ILogger logger, int count);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Error, Message = "Idempotency receipt cleanup failed.")]
    private static partial void LogCleanupFailed(ILogger logger, Exception exception);
}
