using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;
using Pmcs.Modules.Reporting.Rendering;

namespace Pmcs.Modules.Reporting.Services;

/// <summary>Dedicated tenant worker; project-run claims remain in ReportGenerationWorker.</summary>
internal sealed partial class PortfolioReportGenerationWorker(
    IServiceScopeFactory scopeFactory, ReportingRuntimeOptions runtime,
    ReportingExecutionOptions execution, ILogger<PortfolioReportGenerationWorker> logger)
    : BackgroundService
{
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
    private TimeSpan Lease => execution.ProcessingTimeout + TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!runtime.WorkerEnabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                while (await ProcessNextAsync(stoppingToken)) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                LogLoopFailed(logger, exception);
            }
            await Task.Delay(runtime.PollingInterval, stoppingToken);
        }
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        Claimed? claimed;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ReportingDbContext>();
            var now = services.GetRequiredService<IClock>().UtcNow;
            if (await FinalizeExhaustedAsync(db,
                    services.GetRequiredService<ITransactionalSideEffectWriter>(),
                    now, cancellationToken)) return true;
            claimed = await ClaimAsync(db, now, cancellationToken);
        }
        if (claimed is null) return false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(execution.ProcessingTimeout);
        try
        {
            if (claimed.SnapshotWork)
                await BuildSnapshotAsync(claimed.Id, timeout.Token);
            else
                await RenderAsync(claimed.Id, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await RecordFailureAsync(claimed.Id, "reporting.run.timeout", true, cancellationToken);
        }
        catch (ReportRenderingException exception)
        {
            await RecordFailureAsync(claimed.Id, exception.Code, exception.Transient, cancellationToken);
        }
        catch (GeneratedDocumentPublishException exception)
        {
            await RecordFailureAsync(claimed.Id, exception.Code, exception.Transient, cancellationToken);
        }
        catch (DomainRuleException exception)
        {
            await RecordFailureAsync(claimed.Id, exception.Code, false, cancellationToken);
        }
        catch (Exception exception)
        {
            LogRunFailed(logger, exception, claimed.Id);
            await RecordFailureAsync(claimed.Id, claimed.SnapshotWork
                ? "reporting.snapshot.transient" : "reporting.renderer.transient", true,
                cancellationToken);
        }
        return true;
    }

    private async Task<Claimed?> ClaimAsync(ReportingDbContext db, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var pgTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var command = new NpgsqlCommand(
            """
            select id, snapshot_id is null as snapshot_work,
                   pipeline_stage, diagnostic_code is not null as retry_attempt
            from reporting.report_runs
            where scope = 'Portfolio' and definition_code = 'portfolio-summary-certified'
              and output_count = 0 and attempt_count < @maximum_attempts
              and (
                (snapshot_id is null and (
                    (status = 'Queued' and (next_attempt_at is null or next_attempt_at <= @now))
                    or (status = 'Processing' and pipeline_stage = 'BuildingSnapshot'
                        and claimed_at < @lease_cutoff)))
                or (snapshot_id is not null and status = 'Processing' and (
                    (pipeline_stage = 'SnapshotReady' and
                        (next_attempt_at is null or next_attempt_at <= @now))
                    or (pipeline_stage = 'Rendering' and claimed_at < @lease_cutoff)))
              )
            order by created_at, id
            for update skip locked
            limit 1;
            """, connection, pgTransaction);
        command.Parameters.AddWithValue("maximum_attempts", execution.MaximumAttempts);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("lease_cutoff", now.Subtract(Lease));
        Guid? id = null;
        var snapshotWork = false;
        var incrementAttempt = false;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                id = reader.GetGuid(0);
                snapshotWork = reader.GetBoolean(1);
                incrementAttempt = snapshotWork || reader.GetString(2) == "Rendering" ||
                    reader.GetBoolean(3);
            }
        }
        if (!id.HasValue)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        await using var update = new NpgsqlCommand(
            """
            update reporting.report_runs
            set status = 'Processing', pipeline_stage = @stage,
                claimed_at = @now, started_at = coalesce(started_at, @now),
                next_attempt_at = null, diagnostic_code = null, diagnostic_detail = null,
                attempt_count = attempt_count + @attempt_increment, revision = revision + 1
            where id = @id and scope = 'Portfolio';
            """, connection, pgTransaction);
        update.Parameters.AddWithValue("stage", snapshotWork ? "BuildingSnapshot" : "Rendering");
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("attempt_increment", incrementAttempt ? 1 : 0);
        update.Parameters.AddWithValue("id", id.Value);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Portfolio claim was lost.");
        await transaction.CommitAsync(cancellationToken);
        return new Claimed(id.Value, snapshotWork);
    }

    private async Task BuildSnapshotAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ReportingDbContext>();
        var run = await db.Runs.SingleAsync(item => item.Id == runId &&
            item.Scope == ReportDefinitionScope.Portfolio, cancellationToken);
        if (run.Status != ReportRunStatus.Processing ||
            run.PipelineStage != ReportPipelineStage.BuildingSnapshot || run.SnapshotId.HasValue)
            throw Invalid("reporting.run.claim_lost");
        var source = services.GetRequiredService<PortfolioSummaryReportSource>();
        var pinned = Pinned(run);
        var permissionJson = await RequirePermissionsAsync(run, pinned, source,
            services.GetRequiredService<IProjectPermissionService>(), cancellationToken);
        var selected = await source.LoadPinnedAsync(pinned, cancellationToken);
        var now = services.GetRequiredService<IClock>().UtcNow;
        var snapshot = PortfolioSummaryReportSnapshotBuilder.Build(run.Id, selected, now);
        permissionJson = await RequirePermissionsAsync(run, pinned, source,
            services.GetRequiredService<IProjectPermissionService>(), cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        run.AttachSnapshot(snapshot.Id, permissionJson, now);
        await db.SaveChangesAsync(cancellationToken);
        await services.GetRequiredService<ITransactionalSideEffectWriter>().WriteAuditAsync(
            db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            Audit(run, "CertifiedPortfolioReportSnapshotBuilt", now,
                new Dictionary<string, object?>
                {
                    ["snapshotId"] = snapshot.Id,
                    ["snapshotHash"] = snapshot.Sha256,
                    ["sourceManifestHash"] = snapshot.SourceManifestSha256,
                    ["dataStatus"] = snapshot.DataStatus.ToString(),
                    ["attempt"] = run.AttemptCount
                }), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RenderAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ReportingDbContext>();
        var run = await db.Runs.SingleAsync(item => item.Id == runId &&
            item.Scope == ReportDefinitionScope.Portfolio, cancellationToken);
        if (run.Status != ReportRunStatus.Processing ||
            run.PipelineStage != ReportPipelineStage.Rendering || !run.SnapshotId.HasValue ||
            run.OutputCount != 0)
            throw Invalid("reporting.run.claim_lost");
        var source = services.GetRequiredService<PortfolioSummaryReportSource>();
        var pinned = Pinned(run);
        var permissionJson = await RequirePermissionsAsync(run, pinned, source,
            services.GetRequiredService<IProjectPermissionService>(), cancellationToken);
        var snapshot = await db.Snapshots.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == run.SnapshotId.Value && item.RunId == run.Id &&
            item.TenantId == run.TenantId && item.ProjectId == null &&
            item.Scope == ReportDefinitionScope.Portfolio, cancellationToken)
            ?? throw Invalid("reporting.snapshot.missing");
        if (snapshot.SchemaVersion != PortfolioSummaryReportRuntimeContract.SnapshotSchemaVersion ||
            snapshot.SourceCutoffUtc != run.AsOfUtc ||
            snapshot.Classification < ReportClassification.Confidential ||
            CanonicalJson.Sha256(CanonicalJson.Normalize(
                JsonSerializer.Deserialize<JsonElement>(snapshot.PayloadJson))) != snapshot.Sha256 ||
            CanonicalJson.Sha256(CanonicalJson.Normalize(
                JsonSerializer.Deserialize<JsonElement>(snapshot.SourceManifestJson))) !=
                snapshot.SourceManifestSha256)
            throw Invalid("reporting.snapshot.integrity_failed");
        var semantic = PortfolioSummaryReportRenderingContract.Parse(
            snapshot.PayloadJson, snapshot.SourceManifestJson);
        if (semantic.TenantId != run.TenantId || semantic.AsOfUtc != run.AsOfUtc ||
            semantic.DataStatus != snapshot.DataStatus ||
            semantic.Projects.Select(item => item.ProjectId).SequenceEqual(
                pinned.Projects.Select(item => item.ProjectId)) == false ||
            semantic.Projects.Where(item => item.Financial.Status != PortfolioDimensionStatus.NotAuthorized)
                .Select(item => item.ProjectId).SequenceEqual(pinned.Projects
                    .Where(item => item.FinancialAuthorized).Select(item => item.ProjectId)) == false ||
            semantic.Projects.Where(item => item.Commercial.Status != PortfolioDimensionStatus.NotAuthorized)
                .Select(item => item.ProjectId).SequenceEqual(pinned.Projects
                    .Where(item => item.CommercialAuthorized).Select(item => item.ProjectId)) == false)
            throw Invalid("reporting.snapshot.integrity_failed");
        var template = await db.TemplateVersions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == run.TemplateVersionId && item.DefinitionId == run.DefinitionId &&
            item.Version == run.TemplateVersion && !item.RetiredAt.HasValue,
            cancellationToken) ?? throw Invalid("reporting.template.retired");
        var formats = Formats(run.RequestedFormatsJson);
        var renderer = services.GetRequiredService<PortfolioSummaryReportRendererRegistry>();
        var rendered = new List<Prepared>(formats.Length);
        foreach (var format in formats)
        {
            var outputId = ReportArtifactIdentity.OutputId(run.Id, format);
            var documentId = ReportArtifactIdentity.DocumentId(outputId);
            var manifest = ReportArtifactIdentity.CreateManifest(run, snapshot, template, format);
            var name = ReportArtifactIdentity.FileName(semantic, format);
            var artifact = renderer.Require(format).Render(new PortfolioSummaryReportRenderRequest(
                run.Id, outputId, snapshot.Id, template.Id, run.DefinitionCode,
                PortfolioSummaryReportRuntimeContract.DefinitionVersion, run.TemplateVersion,
                template.ContentDigest, template.RendererContractVersion,
                template.LayoutContractVersion, format, name, manifest.VerificationCode,
                manifest.Sha256, snapshot.Sha256, snapshot.SourceManifestSha256,
                snapshot.SourceManifestJson, snapshot.SourceCutoffUtc,
                snapshot.Classification, semantic));
            if (artifact.Bytes.Length == 0 || artifact.Bytes.LongLength > execution.MaximumOutputBytes ||
                artifact.Format != format || artifact.FileName != name ||
                artifact.ContentType != manifest.ContentType ||
                artifact.ManifestSha256 != manifest.Sha256 ||
                artifact.VerificationCode != manifest.VerificationCode ||
                artifact.Sha256 != ReportArtifactIdentity.Sha256(artifact.Bytes))
                throw Invalid("reporting.output.integrity_failed");
            rendered.Add(new Prepared(outputId, documentId, artifact));
        }

        permissionJson = await RequirePermissionsAsync(run, pinned, source,
            services.GetRequiredService<IProjectPermissionService>(), cancellationToken);
        run.RecordRenderingPermissionSnapshot(permissionJson);
        await db.SaveChangesAsync(cancellationToken);
        var publisher = services.GetRequiredService<IGeneratedDocumentPublisher>();
        foreach (var item in rendered)
        {
            var document = await publisher.PublishReportOutputAsync(
                new GeneratedDocumentPublishRequest(item.DocumentId, run.TenantId, null,
                    item.OutputId, item.Artifact.FileName, item.Artifact.ContentType,
                    item.Artifact.Bytes, item.Artifact.Sha256,
                    DocumentClassificationFor(snapshot.Classification),
                    DocumentRetentionPolicy.LongTerm, null, false,
                    services.GetRequiredService<IClock>().UtcNow, run.CorrelationId),
                cancellationToken);
            if (document.DocumentId != item.DocumentId || document.OwnerId != item.OutputId ||
                document.TenantId != run.TenantId || document.ProjectId is not null ||
                document.Sha256 != item.Artifact.Sha256 ||
                document.SizeBytes != item.Artifact.Bytes.LongLength ||
                document.Classification != DocumentClassificationFor(snapshot.Classification) ||
                document.RetentionPolicy != DocumentRetentionPolicy.LongTerm)
                throw Invalid("reporting.output.integrity_failed");
        }
        permissionJson = await RequirePermissionsAsync(run, pinned, source,
            services.GetRequiredService<IProjectPermissionService>(), cancellationToken);
        run.RecordRenderingPermissionSnapshot(permissionJson);
        var now = services.GetRequiredService<IClock>().UtcNow;
        foreach (var item in rendered)
            db.Outputs.Add(ReportOutput.CreatePortfolio(item.OutputId, run.Id, snapshot.Id,
                template.Id, run.TenantId, item.Artifact.Format, item.Artifact.ContentType,
                item.Artifact.FileName, item.DocumentId, item.Artifact.Bytes.LongLength,
                item.Artifact.Sha256, item.Artifact.VerificationCode,
                item.Artifact.ManifestSha256, snapshot.Classification,
                DocumentRetentionPolicy.LongTerm.ToString(), now));
        run.Complete(rendered.Count, now);
        var eventPayload = CanonicalJson.Serialize(new
        {
            runId = run.Id, tenantId = run.TenantId, definitionCode = run.DefinitionCode,
            snapshotSha256 = snapshot.Sha256, sourceManifestSha256 = snapshot.SourceManifestSha256,
            classification = snapshot.Classification,
            outputs = rendered.OrderBy(item => item.Artifact.Format).Select(item => new
            {
                outputId = item.OutputId, format = item.Artifact.Format,
                sha256 = item.Artifact.Sha256, manifestSha256 = item.Artifact.ManifestSha256
            }).ToArray()
        });
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await services.GetRequiredService<ITransactionalSideEffectWriter>().WriteEventAsync(
            db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalEventBatch(
                Audit(run, "CertifiedPortfolioReportRunCompleted", now,
                    new Dictionary<string, object?>
                    {
                        ["snapshotId"] = snapshot.Id,
                        ["snapshotHash"] = snapshot.Sha256,
                        ["outputCount"] = rendered.Count,
                        ["attempt"] = run.AttemptCount
                    }),
                new OutboxEnvelope(Guid.NewGuid(), run.TenantId, null,
                    "reporting.report.completed.v1", 1, now, eventPayload,
                    run.CorrelationId)), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(Guid runId, string code, bool transient,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ReportingDbContext>();
        var run = await db.Runs.SingleOrDefaultAsync(item => item.Id == runId &&
            item.Scope == ReportDefinitionScope.Portfolio, cancellationToken);
        if (run is null || run.Status is ReportRunStatus.Succeeded or ReportRunStatus.Cancelled or
            ReportRunStatus.Failed) return;
        var now = services.GetRequiredService<IClock>().UtcNow;
        if (transient && run.AttemptCount < execution.MaximumAttempts)
        {
            var retry = now.Add(execution.RetryBaseDelay * Math.Max(1, run.AttemptCount));
            if (run.SnapshotId.HasValue) run.RequeueRendering(code, retry);
            else run.Requeue(code, retry);
        }
        else run.Fail(code, null, now);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await services.GetRequiredService<ITransactionalSideEffectWriter>().WriteAuditAsync(
            db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            Audit(run, run.Status == ReportRunStatus.Failed
                ? "CertifiedPortfolioReportRunFailed" : "CertifiedPortfolioReportRunRequeued",
                now, new Dictionary<string, object?>
                {
                    ["diagnosticCode"] = code,
                    ["attempt"] = run.AttemptCount,
                    ["willRetry"] = run.Status != ReportRunStatus.Failed
                }), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> FinalizeExhaustedAsync(ReportingDbContext db,
        ITransactionalSideEffectWriter sideEffects, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var pgTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var command = new NpgsqlCommand(
            """
            select id from reporting.report_runs
            where scope = 'Portfolio' and definition_code = 'portfolio-summary-certified'
              and output_count = 0 and attempt_count >= @maximum_attempts
              and (status = 'Queued' or (status = 'Processing' and
                  (pipeline_stage = 'SnapshotReady' or
                   (pipeline_stage in ('BuildingSnapshot','Rendering')
                    and claimed_at < @lease_cutoff))))
              and (next_attempt_at is null or next_attempt_at <= @now)
            order by created_at, id for update skip locked limit 1;
            """, connection, pgTransaction);
        command.Parameters.AddWithValue("maximum_attempts", execution.MaximumAttempts);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("lease_cutoff", now.Subtract(Lease));
        var selected = await command.ExecuteScalarAsync(cancellationToken);
        if (selected is not Guid id)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }
        var run = await db.Runs.SingleAsync(item => item.Id == id, cancellationToken);
        run.Fail("reporting.retry.exhausted", null, now);
        await db.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAuditAsync(db.Database.GetDbConnection(),
            transaction.GetDbTransaction(), Audit(run, "CertifiedPortfolioReportRunFailed", now,
                new Dictionary<string, object?>
                {
                    ["diagnosticCode"] = "reporting.retry.exhausted",
                    ["attempt"] = run.AttemptCount
                }), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static PortfolioPinnedCohort Pinned(ReportRun run)
    {
        try
        {
            var pinned = JsonSerializer.Deserialize<PortfolioPinnedCohort>(
                run.PinnedPortfolioCohortJson!, CanonicalJson.SerializerOptions)
                ?? throw Invalid("reporting.portfolio.cohort.invalid");
            pinned.Validate();
            if (pinned.TenantId != run.TenantId || pinned.RequestedBy != run.RequestedBy ||
                pinned.AsOfUtc != run.AsOfUtc || run.ProjectId is not null ||
                run.DefinitionCode != PortfolioSummaryReportRuntimeContract.DefinitionCode ||
                run.ParametersJson != "{}")
                throw Invalid("reporting.portfolio.cohort.invalid");
            return pinned;
        }
        catch (JsonException) { throw Invalid("reporting.portfolio.cohort.invalid"); }
    }

    private static async Task<string> RequirePermissionsAsync(ReportRun run,
        PortfolioPinnedCohort pinned, PortfolioSummaryReportSource source,
        IProjectPermissionService permissions, CancellationToken cancellationToken)
    {
        if (!await permissions.HasTenantPermissionAsync(run.TenantId, run.RequestedBy,
                "reporting.run.create", cancellationToken) ||
            !await source.CanAccessPinnedAsync(pinned, run.RequestedBy, cancellationToken))
            throw Invalid("reporting.permission.revoked");
        return CanonicalJson.Serialize(new
        {
            actorUserId = run.RequestedBy, tenantId = run.TenantId,
            cohortSha256 = CanonicalJson.Sha256(CanonicalJson.Serialize(pinned)),
            evaluatedAt = DateTimeOffset.UtcNow, operation = "reporting.run.create"
        });
    }

    private static ReportFormat[] Formats(string json)
    {
        try
        {
            var formats = JsonSerializer.Deserialize<ReportFormat[]>(json, CanonicalJson.SerializerOptions);
            if (formats is null || formats.Length is < 1 or > 2 ||
                formats.Distinct().Count() != formats.Length ||
                formats.Any(item => item is not (ReportFormat.Pdf or ReportFormat.Xlsx)))
                throw Invalid("reporting.format.unsupported");
            return formats.Order().ToArray();
        }
        catch (JsonException) { throw Invalid("reporting.format.unsupported"); }
    }

    private static DocumentClassification DocumentClassificationFor(ReportClassification value) =>
        value switch
        {
            ReportClassification.Confidential => DocumentClassification.Confidential,
            ReportClassification.Restricted => DocumentClassification.Restricted,
            _ => throw Invalid("reporting.output.classification_invalid")
        };

    private static AuditEntry Audit(ReportRun run, string eventType,
        DateTimeOffset at, IReadOnlyDictionary<string, object?> data) =>
        new(run.TenantId, null, SystemActorId, eventType, "ReportRun",
            run.Id.ToString(), at, data, run.CorrelationId);

    private static DomainRuleException Invalid(string code) => new(code,
        "The certified Portfolio report failed its scope, access or integrity contract.");

    [LoggerMessage(EventId = 4201, Level = LogLevel.Error,
        Message = "Portfolio report worker loop failed.")]
    private static partial void LogLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4202, Level = LogLevel.Error,
        Message = "Portfolio report run {RunId} failed unexpectedly.")]
    private static partial void LogRunFailed(ILogger logger, Exception exception, Guid runId);

    private sealed record Claimed(Guid Id, bool SnapshotWork);
    private sealed record Prepared(Guid OutputId, Guid DocumentId, RenderedReportArtifact Artifact);
}
