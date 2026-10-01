using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;

namespace Pmcs.Modules.Reporting.Services;

internal sealed partial class ReportOutputOrphanRemediationWorker(
    IServiceScopeFactory scopeFactory,
    ReportingRuntimeOptions runtime,
    ReportingOrphanRemediationOptions options,
    ILogger<ReportOutputOrphanRemediationWorker> logger) : BackgroundService
{
    private Guid? afterDocumentId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!runtime.Phase1Enabled || options.Mode == ReportingOrphanRemediationMode.Disabled)
        {
            LogDisabled(logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogSweepFailed(logger, exception);
            }

            try
            {
                await Task.Delay(options.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var counters = new SweepCounters();
        var remaining = options.MaximumCandidatesPerSweep;
        var reachedEnd = false;
        while (remaining > 0 && !reachedEnd)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var evaluatedAt = clock.UtcNow;
            var take = Math.Min(options.BatchSize, remaining);
            var remediator = scope.ServiceProvider.GetRequiredService<IGeneratedDocumentOrphanRemediator>();
            var candidates = await remediator.ListReportOutputCandidatesAsync(
                evaluatedAt - options.MinimumAge,
                afterDocumentId,
                take,
                cancellationToken);
            if (candidates.Count == 0)
            {
                afterDocumentId = null;
                reachedEnd = true;
                break;
            }

            foreach (var candidate in candidates)
            {
                counters.Scanned++;
                try
                {
                    var outcome = await ProcessCandidateAsync(
                        scope.ServiceProvider,
                        remediator,
                        candidate,
                        evaluatedAt,
                        cancellationToken);
                    counters.Record(outcome);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    counters.Failed++;
                    LogCandidateFailed(logger, exception);
                }
            }

            remaining -= candidates.Count;
            afterDocumentId = candidates[^1].DocumentId;
            if (candidates.Count < take)
            {
                afterDocumentId = null;
                reachedEnd = true;
            }
        }

        LogSweepCompleted(
            logger,
            options.Mode,
            counters.Scanned,
            counters.Orphans,
            counters.Eligible,
            counters.Remediated,
            counters.RetentionProtected,
            counters.LegalHoldProtected,
            counters.Owned,
            counters.Recoverable,
            counters.Ambiguous,
            counters.Failed);
    }

    private async Task<OrphanCandidateOutcome> ProcessCandidateAsync(
        IServiceProvider services,
        IGeneratedDocumentOrphanRemediator remediator,
        GeneratedDocumentOrphanCandidate candidate,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<ReportingDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await OwnerExistsAsync(dbContext, candidate, cancellationToken))
        {
            return OrphanCandidateOutcome.Owned;
        }

        var lineage = await FindLineageAsync(dbContext, candidate, cancellationToken);
        if (lineage.Count != 1)
        {
            return OrphanCandidateOutcome.Ambiguous;
        }

        var runId = lineage[0];
        await ReportRunAdvisoryLock.AcquireAsync(dbContext, runId, cancellationToken);
        var run = await dbContext.Runs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == runId &&
            item.TenantId == candidate.TenantId &&
            item.ProjectId == candidate.ProjectId,
            cancellationToken);
        if (run is null ||
            run.Status != ReportRunStatus.Failed ||
            run.PipelineStage != ReportPipelineStage.Failed ||
            run.OutputCount != 0 ||
            await OwnerExistsAsync(dbContext, candidate, cancellationToken))
        {
            return OrphanCandidateOutcome.Recoverable;
        }
        if (candidate.LegalHold)
        {
            return OrphanCandidateOutcome.LegalHoldProtected;
        }
        if (candidate.RetentionPolicy is null or DocumentRetentionPolicy.Permanent ||
            !candidate.RetainUntil.HasValue || candidate.RetainUntil.Value > evaluatedAt)
        {
            return OrphanCandidateOutcome.RetentionProtected;
        }
        if (options.Mode == ReportingOrphanRemediationMode.InventoryOnly)
        {
            return OrphanCandidateOutcome.Eligible;
        }

        var result = await remediator.RemediateReportOutputAsync(
            new GeneratedDocumentOrphanRemediationRequest(
                candidate.DocumentId,
                candidate.TenantId,
                candidate.ProjectId,
                candidate.OwnerId,
                run.Id,
                candidate.Revision,
                evaluatedAt,
                run.CorrelationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result switch
        {
            GeneratedDocumentOrphanRemediationResult.Remediated => OrphanCandidateOutcome.Remediated,
            GeneratedDocumentOrphanRemediationResult.NotFound => OrphanCandidateOutcome.AlreadyAbsent,
            GeneratedDocumentOrphanRemediationResult.LegalHold => OrphanCandidateOutcome.LegalHoldProtected,
            GeneratedDocumentOrphanRemediationResult.RetentionActive => OrphanCandidateOutcome.RetentionProtected,
            _ => OrphanCandidateOutcome.Changed
        };
    }

    private static Task<bool> OwnerExistsAsync(
        ReportingDbContext dbContext,
        GeneratedDocumentOrphanCandidate candidate,
        CancellationToken cancellationToken) =>
        dbContext.Outputs.AsNoTracking().AnyAsync(output =>
            output.TenantId == candidate.TenantId &&
            output.ProjectId == candidate.ProjectId &&
            (output.Id == candidate.OwnerId || output.GeneratedDocumentId == candidate.DocumentId),
            cancellationToken);

    private static async Task<IReadOnlyList<Guid>> FindLineageAsync(
        ReportingDbContext dbContext,
        GeneratedDocumentOrphanCandidate candidate,
        CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction)dbContext.Database.CurrentTransaction!.GetDbTransaction();
        await using var command = new NpgsqlCommand(
            """
            select distinct run.id
            from foundation.audit_events event
            join reporting.report_runs run
              on run.correlation_id = event.correlation_id
             and run.tenant_id = event.tenant_id
             and run.project_id = event.project_id
            where event.tenant_id = @tenant_id
              and event.project_id = @project_id
              and event.event_type = 'GeneratedReportDocumentReleased'
              and event.resource_type = 'DocumentAsset'
              and event.resource_id = @document_id
              and event.data ->> 'ownerId' = @owner_id
            order by run.id
            limit 2;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("tenant_id", candidate.TenantId);
        command.Parameters.AddWithValue("project_id", candidate.ProjectId);
        command.Parameters.AddWithValue("document_id", candidate.DocumentId.ToString());
        command.Parameters.AddWithValue("owner_id", candidate.OwnerId.ToString());
        var runIds = new List<Guid>(2);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            runIds.Add(reader.GetGuid(0));
        }
        return runIds;
    }

    [LoggerMessage(EventId = 3301, Level = LogLevel.Information,
        Message = "Report-output orphan remediation is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Information,
        Message = "Report-output orphan sweep completed: mode={Mode}, scanned={Scanned}, orphans={Orphans}, eligible={Eligible}, remediated={Remediated}, retentionProtected={RetentionProtected}, legalHoldProtected={LegalHoldProtected}, owned={Owned}, recoverable={Recoverable}, ambiguous={Ambiguous}, failed={Failed}.")]
    private static partial void LogSweepCompleted(
        ILogger logger,
        ReportingOrphanRemediationMode mode,
        int scanned,
        int orphans,
        int eligible,
        int remediated,
        int retentionProtected,
        int legalHoldProtected,
        int owned,
        int recoverable,
        int ambiguous,
        int failed);

    [LoggerMessage(EventId = 3303, Level = LogLevel.Error,
        Message = "Report-output orphan sweep failed.")]
    private static partial void LogSweepFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3304, Level = LogLevel.Warning,
        Message = "A report-output orphan candidate could not be evaluated.")]
    private static partial void LogCandidateFailed(ILogger logger, Exception exception);

    private enum OrphanCandidateOutcome
    {
        Owned,
        Recoverable,
        Ambiguous,
        RetentionProtected,
        LegalHoldProtected,
        Eligible,
        Remediated,
        AlreadyAbsent,
        Changed
    }

    private sealed class SweepCounters
    {
        public int Scanned { get; set; }
        public int Orphans { get; private set; }
        public int Eligible { get; private set; }
        public int Remediated { get; private set; }
        public int RetentionProtected { get; private set; }
        public int LegalHoldProtected { get; private set; }
        public int Owned { get; private set; }
        public int Recoverable { get; private set; }
        public int Ambiguous { get; private set; }
        public int Failed { get; set; }

        public void Record(OrphanCandidateOutcome outcome)
        {
            switch (outcome)
            {
                case OrphanCandidateOutcome.Owned:
                    Owned++;
                    return;
                case OrphanCandidateOutcome.Recoverable:
                    Orphans++;
                    Recoverable++;
                    return;
                case OrphanCandidateOutcome.Ambiguous:
                    Orphans++;
                    Ambiguous++;
                    return;
                case OrphanCandidateOutcome.RetentionProtected:
                    Orphans++;
                    RetentionProtected++;
                    return;
                case OrphanCandidateOutcome.LegalHoldProtected:
                    Orphans++;
                    LegalHoldProtected++;
                    return;
                case OrphanCandidateOutcome.Eligible:
                    Orphans++;
                    Eligible++;
                    return;
                case OrphanCandidateOutcome.Remediated:
                    Orphans++;
                    Eligible++;
                    Remediated++;
                    return;
                case OrphanCandidateOutcome.AlreadyAbsent:
                case OrphanCandidateOutcome.Changed:
                    Orphans++;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }
        }
    }
}
