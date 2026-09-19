using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;

namespace Pmcs.Modules.Reporting.Services;

internal sealed class ReportingWorkerHealthCheck(
    IServiceScopeFactory scopeFactory,
    ReportingRuntimeOptions runtime,
    ReportingExecutionOptions execution,
    ReportingWorkerTelemetry telemetry) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!runtime.WorkerEnabled)
        {
            return HealthCheckResult.Healthy("Reporting worker is disabled by feature flags.");
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            var pending = dbContext.Runs.AsNoTracking().Where(run =>
                run.Status == ReportRunStatus.Queued ||
                (run.Status == ReportRunStatus.Processing &&
                    run.PipelineStage == ReportPipelineStage.SnapshotReady));
            var queueCount = await pending.LongCountAsync(cancellationToken);
            var oldestCreatedAt = await pending
                .MinAsync(run => (DateTimeOffset?)run.CreatedAt, cancellationToken);
            var oldestAge = oldestCreatedAt.HasValue
                ? now - oldestCreatedAt.Value
                : TimeSpan.Zero;
            telemetry.ObserveQueue(queueCount, oldestAge);

            var heartbeat = telemetry.LastHeartbeatUtc;
            var heartbeatAge = heartbeat.HasValue ? now - heartbeat.Value : (TimeSpan?)null;
            var heartbeatWarning = execution.QueueAgeWarning > runtime.PollingInterval * 3
                ? execution.QueueAgeWarning
                : runtime.PollingInterval * 3;
            var data = new Dictionary<string, object>
            {
                ["activeRuns"] = telemetry.ActiveRuns,
                ["queuedRuns"] = queueCount,
                ["oldestQueueAgeSeconds"] = Math.Max(0, oldestAge.TotalSeconds),
                ["heartbeatAgeSeconds"] = heartbeatAge.HasValue
                    ? Math.Max(0, heartbeatAge.Value.TotalSeconds)
                    : -1
            };

            if (!heartbeatAge.HasValue || heartbeatAge > heartbeatWarning)
            {
                return HealthCheckResult.Degraded(
                    "Reporting worker heartbeat is missing or stale.",
                    data: data);
            }
            if (queueCount > 0 && oldestAge > execution.QueueAgeWarning)
            {
                return HealthCheckResult.Degraded(
                    "Reporting queue age is above the configured warning budget.",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Reporting worker heartbeat and queue age are within budget.",
                data);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Reporting worker health could not be evaluated.",
                exception);
        }
    }
}
