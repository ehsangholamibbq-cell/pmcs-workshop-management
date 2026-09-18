using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Endpoints;
using Pmcs.Modules.Reporting.Persistence;

namespace Pmcs.Modules.Reporting.Services;

internal sealed partial class ReportGenerationWorker(
    IServiceScopeFactory scopeFactory,
    ReportingRuntimeOptions runtime,
    ILogger<ReportGenerationWorker> logger) : BackgroundService
{
    private const int MaximumAttempts = 3;
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!runtime.WorkerEnabled)
        {
            LogDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(stoppingToken))
                {
                    // Drain all currently eligible runs before waiting for the next poll.
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogLoopFailed(logger, exception);
            }

            await Task.Delay(runtime.PollingInterval, stoppingToken);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        ClaimedRun? claimed;
        await using (var claimScope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = claimScope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            var clock = claimScope.ServiceProvider.GetRequiredService<IClock>();
            claimed = await ClaimNextAsync(dbContext, clock.UtcNow, cancellationToken);
        }

        if (claimed is null)
        {
            return false;
        }

        try
        {
            await BuildSnapshotAsync(claimed, cancellationToken);
            LogSnapshotBuilt(logger, claimed.Id, claimed.ProjectId);
        }
        catch (ReportProcessingException exception)
        {
            await RecordFailureAsync(
                claimed,
                exception.Code,
                exception.Transient,
                exception.PermissionSnapshotJson,
                cancellationToken);
            LogRunFailed(logger, claimed.Id, exception.Code);
        }
        catch (DomainRuleException exception)
        {
            await RecordFailureAsync(
                claimed,
                exception.Code,
                transient: false,
                permissionSnapshotJson: null,
                cancellationToken: cancellationToken);
            LogRunFailed(logger, claimed.Id, exception.Code);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFailureAsync(
                claimed,
                "reporting.snapshot.transient",
                transient: true,
                permissionSnapshotJson: null,
                cancellationToken: cancellationToken);
            LogUnexpectedRunFailure(logger, exception, claimed.Id);
        }

        return true;
    }

    private async Task BuildSnapshotAsync(ClaimedRun claimed, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var permissionService = services.GetRequiredService<IProjectPermissionService>();
        var projectDirectory = services.GetRequiredService<IProjectDirectory>();
        var source = services.GetRequiredService<IDailyReportReportingSource>();
        var snapshotBuilder = services.GetRequiredService<DailyReportSnapshotBuilder>();
        var dbContext = services.GetRequiredService<ReportingDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var sideEffectWriter = services.GetRequiredService<ITransactionalSideEffectWriter>();

        var preview = await permissionService.PreviewProjectPermissionsAsync(
            claimed.TenantId,
            claimed.RequestedBy,
            claimed.ProjectId,
            operations: ["reporting.run.create", "field.daily-reports.read"],
            cancellationToken: cancellationToken);
        var permissionSnapshotJson = ReportingEndpoints.SerializePermissionSnapshot(preview);
        if (preview.Decisions.Any(decision => !decision.Allowed))
        {
            throw new ReportProcessingException(
                "reporting.permission.revoked",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }

        var run = await dbContext.Runs.SingleOrDefaultAsync(item =>
            item.Id == claimed.Id &&
            item.TenantId == claimed.TenantId &&
            item.ProjectId == claimed.ProjectId,
            cancellationToken);
        if (run is null || run.Status != ReportRunStatus.Processing ||
            run.PipelineStage != ReportPipelineStage.BuildingSnapshot || run.SnapshotId.HasValue)
        {
            throw new ReportProcessingException(
                "reporting.run.claim_lost",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }
        run.RecordProcessingPermissionSnapshot(permissionSnapshotJson);
        await dbContext.SaveChangesAsync(cancellationToken);

        var project = await projectDirectory.FindProfileAsync(
            claimed.TenantId,
            claimed.ProjectId,
            cancellationToken);
        if (project is null || project.Status != ProjectStatus.Active)
        {
            throw new ReportProcessingException(
                "reporting.project.not_operational",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }

        var parameters = JsonSerializer.Deserialize<DailyReportReportParameters>(
            claimed.ParametersJson,
            CanonicalJson.SerializerOptions)
            ?? throw new ReportProcessingException(
                "reporting.parameters.invalid",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        var chain = await source.LoadChainAsync(
            claimed.TenantId,
            claimed.ProjectId,
            parameters.DailyReportId,
            claimed.AsOfUtc,
            cancellationToken);
        var builtAt = clock.UtcNow;
        var snapshot = snapshotBuilder.Build(
            claimed.Id,
            claimed.TenantId,
            project,
            claimed.AsOfUtc,
            parameters,
            chain,
            builtAt);

        dbContext.Snapshots.Add(snapshot);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        run.AttachSnapshot(snapshot.Id, permissionSnapshotJson, builtAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAuditAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new AuditEntry(
                claimed.TenantId,
                claimed.ProjectId,
                Guid.Empty,
                "CertifiedReportSnapshotBuilt",
                "ReportRun",
                claimed.Id.ToString(),
                builtAt,
                new Dictionary<string, object?>
                {
                    ["executor"] = "SystemWorker",
                    ["requestedBy"] = claimed.RequestedBy,
                    ["snapshotId"] = snapshot.Id,
                    ["snapshotHash"] = snapshot.Sha256,
                    ["sourceManifestHash"] = snapshot.SourceManifestSha256,
                    ["dataStatus"] = snapshot.DataStatus.ToString(),
                    ["asOfUtc"] = snapshot.SourceCutoffUtc,
                    ["attempt"] = claimed.AttemptCount
                },
                claimed.CorrelationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<ClaimedRun?> ClaimNextAsync(
        ReportingDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var select = new NpgsqlCommand(
            """
            select id, tenant_id, project_id, requested_by, parameters_json::text,
                   as_of_utc, attempt_count, correlation_id
            from reporting.report_runs
            where snapshot_id is null
              and attempt_count < @maximum_attempts
              and (
                  (status = 'Queued' and (next_attempt_at is null or next_attempt_at <= @now))
                  or
                  (status = 'Processing' and pipeline_stage = 'BuildingSnapshot'
                      and claimed_at < @lease_cutoff)
              )
            order by created_at, id
            for update skip locked
            limit 1;
            """,
            connection,
            postgresTransaction);
        select.Parameters.AddWithValue("maximum_attempts", MaximumAttempts);
        select.Parameters.AddWithValue("now", now);
        select.Parameters.AddWithValue("lease_cutoff", now.Subtract(ProcessingLease));

        ClaimedRun? claimed = null;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                claimed = new ClaimedRun(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetGuid(3),
                    reader.GetString(4),
                    reader.GetFieldValue<DateTimeOffset>(5),
                    reader.GetInt32(6) + 1,
                    reader.GetString(7));
            }
        }

        if (claimed is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await using var update = new NpgsqlCommand(
            """
            update reporting.report_runs
            set status = 'Processing',
                pipeline_stage = 'BuildingSnapshot',
                claimed_at = @now,
                started_at = coalesce(started_at, @now),
                next_attempt_at = null,
                diagnostic_code = null,
                diagnostic_detail = null,
                attempt_count = attempt_count + 1,
                revision = revision + 1
            where id = @id;
            """,
            connection,
            postgresTransaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("id", claimed.Id);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private async Task RecordFailureAsync(
        ClaimedRun claimed,
        string code,
        bool transient,
        string? permissionSnapshotJson,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var sideEffectWriter = scope.ServiceProvider.GetRequiredService<ITransactionalSideEffectWriter>();
        var run = await dbContext.Runs.SingleOrDefaultAsync(item =>
            item.Id == claimed.Id && item.TenantId == claimed.TenantId,
            cancellationToken);
        if (run is null || run.SnapshotId.HasValue || run.Status is ReportRunStatus.Succeeded or ReportRunStatus.Cancelled)
        {
            return;
        }

        var now = clock.UtcNow;
        if (transient && run.AttemptCount < MaximumAttempts)
        {
            run.Requeue(code, now.AddSeconds(30 * Math.Max(1, run.AttemptCount)));
        }
        else
        {
            run.Fail(
                code,
                diagnosticDetail: null,
                failedAt: now,
                processingPermissionSnapshotJson: permissionSnapshotJson);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAuditAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new AuditEntry(
                claimed.TenantId,
                claimed.ProjectId,
                Guid.Empty,
                transient && run.Status == ReportRunStatus.Queued
                    ? "CertifiedReportSnapshotRequeued"
                    : "CertifiedReportRunFailed",
                "ReportRun",
                claimed.Id.ToString(),
                now,
                new Dictionary<string, object?>
                {
                    ["executor"] = "SystemWorker",
                    ["requestedBy"] = claimed.RequestedBy,
                    ["diagnosticCode"] = code,
                    ["attempt"] = run.AttemptCount,
                    ["willRetry"] = run.Status == ReportRunStatus.Queued
                },
                claimed.CorrelationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record ClaimedRun(
        Guid Id,
        Guid TenantId,
        Guid ProjectId,
        Guid RequestedBy,
        string ParametersJson,
        DateTimeOffset AsOfUtc,
        int AttemptCount,
        string CorrelationId);

    private sealed class ReportProcessingException(
        string code,
        bool transient,
        string? permissionSnapshotJson) : Exception(code)
    {
        public string Code { get; } = code;
        public bool Transient { get; } = transient;
        public string? PermissionSnapshotJson { get; } = permissionSnapshotJson;
    }

    [LoggerMessage(
        EventId = 6100,
        Level = LogLevel.Information,
        Message = "Reporting snapshot worker is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Error,
        Message = "Reporting snapshot worker loop failed.")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 6102,
        Level = LogLevel.Information,
        Message = "Reporting snapshot built for run {RunId} in project {ProjectId}.")]
    private static partial void LogSnapshotBuilt(ILogger logger, Guid runId, Guid projectId);

    [LoggerMessage(
        EventId = 6103,
        Level = LogLevel.Warning,
        Message = "Reporting run {RunId} failed with code {Code}.")]
    private static partial void LogRunFailed(ILogger logger, Guid runId, string code);

    [LoggerMessage(
        EventId = 6104,
        Level = LogLevel.Error,
        Message = "Reporting run {RunId} failed unexpectedly.")]
    private static partial void LogUnexpectedRunFailure(ILogger logger, Exception exception, Guid runId);
}
