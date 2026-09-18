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
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Endpoints;
using Pmcs.Modules.Reporting.Persistence;
using Pmcs.Modules.Reporting.Rendering;

namespace Pmcs.Modules.Reporting.Services;

internal sealed partial class ReportGenerationWorker(
    IServiceScopeFactory scopeFactory,
    ReportingRuntimeOptions runtime,
    ILogger<ReportGenerationWorker> logger) : BackgroundService
{
    internal const int MaximumAttempts = 3;
    private const long MaximumOutputBytes = 25L * 1024L * 1024L;
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
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
            claimed = await ClaimNextSnapshotAsync(dbContext, clock.UtcNow, cancellationToken)
                ?? await ClaimNextRenderingAsync(dbContext, clock.UtcNow, cancellationToken);
        }

        if (claimed is null)
        {
            return false;
        }

        var snapshotBuilt = claimed.WorkKind == ReportWorkKind.Rendering;
        try
        {
            if (claimed.WorkKind == ReportWorkKind.Snapshot)
            {
                await BuildSnapshotAsync(claimed, cancellationToken);
                snapshotBuilt = true;
                LogSnapshotBuilt(logger, claimed.Id, claimed.ProjectId);
            }

            await RenderOutputsAsync(claimed, cancellationToken);
            LogRunCompleted(logger, claimed.Id, claimed.ProjectId);
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
        catch (GeneratedDocumentPublishException exception)
        {
            await RecordFailureAsync(
                claimed,
                exception.Code,
                exception.Transient,
                permissionSnapshotJson: null,
                cancellationToken);
            LogRunFailed(logger, claimed.Id, exception.Code);
        }
        catch (ReportRenderingException exception)
        {
            await RecordFailureAsync(
                claimed,
                exception.Code,
                exception.Transient,
                permissionSnapshotJson: null,
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
                snapshotBuilt
                    ? "reporting.renderer.transient"
                    : "reporting.snapshot.transient",
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

        var permissionSnapshotJson = await RequireProcessingPermissionsAsync(
            permissionService,
            claimed,
            cancellationToken);
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

        var project = await RequireActiveProjectAsync(
            projectDirectory,
            claimed,
            permissionSnapshotJson,
            cancellationToken);
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
                SystemActorId,
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

    private async Task RenderOutputsAsync(ClaimedRun claimed, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var permissionService = services.GetRequiredService<IProjectPermissionService>();
        var projectDirectory = services.GetRequiredService<IProjectDirectory>();
        var dbContext = services.GetRequiredService<ReportingDbContext>();
        var rendererRegistry = services.GetRequiredService<ReportRendererRegistry>();
        var publisher = services.GetRequiredService<IGeneratedDocumentPublisher>();
        var sideEffectWriter = services.GetRequiredService<ITransactionalSideEffectWriter>();
        var clock = services.GetRequiredService<IClock>();

        var permissionSnapshotJson = await RequireProcessingPermissionsAsync(
            permissionService,
            claimed,
            cancellationToken);
        _ = await RequireActiveProjectAsync(
            projectDirectory,
            claimed,
            permissionSnapshotJson,
            cancellationToken);

        var run = await dbContext.Runs.SingleOrDefaultAsync(item =>
            item.Id == claimed.Id &&
            item.TenantId == claimed.TenantId &&
            item.ProjectId == claimed.ProjectId,
            cancellationToken);
        if (run is null || run.Status != ReportRunStatus.Processing || !run.SnapshotId.HasValue ||
            run.PipelineStage is not (ReportPipelineStage.SnapshotReady or ReportPipelineStage.Rendering))
        {
            throw new ReportProcessingException(
                "reporting.run.claim_lost",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }

        var snapshot = await dbContext.Snapshots.SingleOrDefaultAsync(item =>
            item.Id == run.SnapshotId.Value &&
            item.RunId == run.Id &&
            item.TenantId == run.TenantId &&
            item.ProjectId == run.ProjectId,
            cancellationToken) ?? throw new ReportProcessingException(
                "reporting.snapshot.missing",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        VerifySnapshot(snapshot, run);
        var renderSnapshot = DailyReportRenderSnapshot.Parse(snapshot.PayloadJson);
        VerifyRenderSnapshot(renderSnapshot, snapshot, run);
        var template = await dbContext.TemplateVersions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == run.TemplateVersionId &&
                item.DefinitionId == run.DefinitionId &&
                item.Version == run.TemplateVersion,
            cancellationToken);
        if (template is null || template.RetiredAt.HasValue)
        {
            throw new ReportProcessingException(
                "reporting.template.retired",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }

        var resumed = run.PipelineStage == ReportPipelineStage.Rendering;
        var renderStartedAt = clock.UtcNow;
        await using (var startTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            if (!resumed)
            {
                run.BeginRendering(renderStartedAt);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await sideEffectWriter.WriteAuditAsync(
                dbContext.Database.GetDbConnection(),
                startTransaction.GetDbTransaction(),
                new AuditEntry(
                    run.TenantId,
                    run.ProjectId,
                    SystemActorId,
                    resumed ? "CertifiedReportRenderingResumed" : "CertifiedReportRenderingStarted",
                    "ReportRun",
                    run.Id.ToString(),
                    renderStartedAt,
                    new Dictionary<string, object?>
                    {
                        ["executor"] = "SystemWorker",
                        ["requestedBy"] = run.RequestedBy,
                        ["snapshotId"] = snapshot.Id,
                        ["snapshotHash"] = snapshot.Sha256,
                        ["attempt"] = run.AttemptCount
                    },
                    run.CorrelationId),
                cancellationToken);
            await startTransaction.CommitAsync(cancellationToken);
        }

        var formats = DeserializeFormats(run.RequestedFormatsJson);
        var rendered = new List<PreparedArtifact>(formats.Length);
        foreach (var format in formats)
        {
            var outputId = ReportArtifactIdentity.OutputId(run.Id, format);
            var documentId = ReportArtifactIdentity.DocumentId(outputId);
            var manifest = ReportArtifactIdentity.CreateManifest(run, snapshot, template, format);
            var fileName = ReportArtifactIdentity.FileName(renderSnapshot, format);
            var renderRequest = new ReportRenderRequest(
                run.Id,
                outputId,
                snapshot.Id,
                template.Id,
                run.DefinitionCode,
                run.TemplateVersion,
                template.ContentDigest,
                template.RendererContractVersion,
                template.LayoutContractVersion,
                format,
                fileName,
                manifest.VerificationCode,
                manifest.Sha256,
                snapshot.Sha256,
                snapshot.SourceManifestSha256,
                snapshot.SourceCutoffUtc,
                renderSnapshot);
            var artifact = rendererRegistry.Require(format).Render(renderRequest);
            VerifyRenderedArtifact(artifact, manifest, fileName, format);
            rendered.Add(new PreparedArtifact(outputId, documentId, artifact));
        }

        foreach (var item in rendered)
        {
            var document = await publisher.PublishReportOutputAsync(
                new GeneratedDocumentPublishRequest(
                    item.DocumentId,
                    run.TenantId,
                    run.ProjectId,
                    item.OutputId,
                    item.Artifact.FileName,
                    item.Artifact.ContentType,
                    item.Artifact.Bytes,
                    item.Artifact.Sha256,
                    ToDocumentClassification(snapshot.Classification),
                    DocumentRetentionPolicy.LongTerm,
                    RetainUntil: null,
                    LegalHold: false,
                    CreatedAt: clock.UtcNow,
                    CorrelationId: run.CorrelationId),
                cancellationToken);
            if (document.DocumentId != item.DocumentId || document.OwnerId != item.OutputId ||
                document.TenantId != run.TenantId || document.ProjectId != run.ProjectId ||
                !string.Equals(document.FileName, item.Artifact.FileName, StringComparison.Ordinal) ||
                document.SizeBytes != item.Artifact.Bytes.LongLength ||
                !string.Equals(document.Sha256, item.Artifact.Sha256, StringComparison.Ordinal) ||
                !string.Equals(document.ContentType, item.Artifact.ContentType, StringComparison.OrdinalIgnoreCase) ||
                document.Classification != ToDocumentClassification(snapshot.Classification) ||
                document.RetentionPolicy != DocumentRetentionPolicy.LongTerm)
            {
                throw new ReportProcessingException(
                    "reporting.output.integrity_failed",
                    transient: false,
                    permissionSnapshotJson: permissionSnapshotJson);
            }
        }

        var completedAt = clock.UtcNow;
        foreach (var item in rendered)
        {
            dbContext.Outputs.Add(ReportOutput.Create(
                item.OutputId,
                run.Id,
                snapshot.Id,
                template.Id,
                run.TenantId,
                run.ProjectId,
                item.Artifact.Format,
                item.Artifact.ContentType,
                item.Artifact.FileName,
                item.DocumentId,
                item.Artifact.Bytes.LongLength,
                item.Artifact.Sha256,
                item.Artifact.VerificationCode,
                item.Artifact.ManifestSha256,
                snapshot.Classification,
                DocumentRetentionPolicy.LongTerm.ToString(),
                completedAt));
        }
        run.Complete(rendered.Count, completedAt);
        var eventPayload = CanonicalJson.Serialize(new
        {
            runId = run.Id,
            projectId = run.ProjectId,
            definitionCode = run.DefinitionCode,
            templateVersion = run.TemplateVersion,
            dataStatus = snapshot.DataStatus,
            snapshotSha256 = snapshot.Sha256,
            sourceManifestSha256 = snapshot.SourceManifestSha256,
            classification = snapshot.Classification,
            outputs = rendered.OrderBy(item => item.Artifact.Format).Select(item => new
            {
                outputId = item.OutputId,
                format = item.Artifact.Format,
                sha256 = item.Artifact.Sha256,
                manifestSha256 = item.Artifact.ManifestSha256
            }).ToArray()
        });
        await using var completionTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteEventAsync(
            dbContext.Database.GetDbConnection(),
            completionTransaction.GetDbTransaction(),
            new TransactionalEventBatch(
                new AuditEntry(
                    run.TenantId,
                    run.ProjectId,
                    SystemActorId,
                    "CertifiedReportRunCompleted",
                    "ReportRun",
                    run.Id.ToString(),
                    completedAt,
                    new Dictionary<string, object?>
                    {
                        ["executor"] = "SystemWorker",
                        ["requestedBy"] = run.RequestedBy,
                        ["snapshotId"] = snapshot.Id,
                        ["snapshotHash"] = snapshot.Sha256,
                        ["outputCount"] = rendered.Count,
                        ["formats"] = rendered.Select(item => item.Artifact.Format.ToString()).ToArray(),
                        ["attempt"] = run.AttemptCount
                    },
                    run.CorrelationId),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    run.TenantId,
                    run.ProjectId,
                    "reporting.report.completed.v1",
                    1,
                    completedAt,
                    eventPayload,
                    run.CorrelationId)),
            cancellationToken);
        await completionTransaction.CommitAsync(cancellationToken);
    }

    private static async Task<ClaimedRun?> ClaimNextSnapshotAsync(
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

        var claimed = await ReadClaimAsync(select, ReportWorkKind.Snapshot, incrementAttempt: true, cancellationToken);
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

    private static async Task<ClaimedRun?> ClaimNextRenderingAsync(
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
                   as_of_utc, attempt_count, correlation_id,
                   (pipeline_stage = 'Rendering' or diagnostic_code is not null) as retry_attempt
            from reporting.report_runs
            where status = 'Processing'
              and snapshot_id is not null
              and output_count = 0
              and (
                  (pipeline_stage = 'SnapshotReady'
                      and (next_attempt_at is null or next_attempt_at <= @now)
                      and (diagnostic_code is null or attempt_count < @maximum_attempts))
                  or
                  (pipeline_stage = 'Rendering'
                      and claimed_at < @lease_cutoff
                      and attempt_count < @maximum_attempts)
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
        var retryAttempt = false;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                retryAttempt = reader.GetBoolean(8);
                claimed = CreateClaim(reader, ReportWorkKind.Rendering, retryAttempt);
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
            set pipeline_stage = 'Rendering',
                claimed_at = @now,
                next_attempt_at = null,
                diagnostic_code = null,
                diagnostic_detail = null,
                attempt_count = attempt_count + @attempt_increment,
                revision = revision + 1
            where id = @id;
            """,
            connection,
            postgresTransaction);
        update.Parameters.AddWithValue("now", now);
        update.Parameters.AddWithValue("attempt_increment", retryAttempt ? 1 : 0);
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
        if (run is null || run.Status is ReportRunStatus.Succeeded or ReportRunStatus.Cancelled or ReportRunStatus.Failed)
        {
            return;
        }

        var now = clock.UtcNow;
        if (transient && run.AttemptCount < MaximumAttempts)
        {
            var retryAt = now.AddSeconds(30 * Math.Max(1, run.AttemptCount));
            if (run.SnapshotId.HasValue)
            {
                run.RequeueRendering(code, retryAt);
            }
            else
            {
                run.Requeue(code, retryAt);
            }
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
                SystemActorId,
                transient && (run.Status is ReportRunStatus.Queued or ReportRunStatus.Processing)
                    ? "CertifiedReportRunRequeued"
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
                    ["willRetry"] = run.Status is ReportRunStatus.Queued or ReportRunStatus.Processing,
                    ["stage"] = run.PipelineStage.ToString()
                },
                claimed.CorrelationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<string> RequireProcessingPermissionsAsync(
        IProjectPermissionService permissionService,
        ClaimedRun claimed,
        CancellationToken cancellationToken)
    {
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
        return permissionSnapshotJson;
    }

    private static async Task<ProjectControlProfile> RequireActiveProjectAsync(
        IProjectDirectory projectDirectory,
        ClaimedRun claimed,
        string permissionSnapshotJson,
        CancellationToken cancellationToken)
    {
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
        return project;
    }

    private static void VerifySnapshot(ReportSnapshot snapshot, ReportRun run)
    {
        var payload = JsonSerializer.Deserialize<JsonElement>(snapshot.PayloadJson);
        var sourceManifest = JsonSerializer.Deserialize<JsonElement>(snapshot.SourceManifestJson);
        var payloadHash = CanonicalJson.Sha256(CanonicalJson.Normalize(payload));
        var sourceHash = CanonicalJson.Sha256(CanonicalJson.Normalize(sourceManifest));
        if (!string.Equals(payloadHash, snapshot.Sha256, StringComparison.Ordinal) ||
            !string.Equals(sourceHash, snapshot.SourceManifestSha256, StringComparison.Ordinal) ||
            snapshot.SourceCutoffUtc != run.AsOfUtc)
        {
            throw new ReportProcessingException(
                "reporting.snapshot.integrity_failed",
                transient: false,
                permissionSnapshotJson: null);
        }
    }

    private static void VerifyRenderSnapshot(
        DailyReportRenderSnapshot renderSnapshot,
        ReportSnapshot snapshot,
        ReportRun run)
    {
        if (!string.Equals(renderSnapshot.SchemaVersion, snapshot.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(renderSnapshot.DefinitionCode, run.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(renderSnapshot.TemplateVersion, run.TemplateVersion, StringComparison.Ordinal) ||
            renderSnapshot.Project.Id != run.ProjectId || renderSnapshot.DataStatus != snapshot.DataStatus ||
            renderSnapshot.AsOfUtc != run.AsOfUtc ||
            !string.Equals(renderSnapshot.SourceManifestSha256, snapshot.SourceManifestSha256, StringComparison.Ordinal))
        {
            throw new ReportProcessingException(
                "reporting.snapshot.integrity_failed",
                transient: false,
                permissionSnapshotJson: null);
        }
    }

    private static void VerifyRenderedArtifact(
        RenderedReportArtifact artifact,
        ReportArtifactManifest manifest,
        string expectedFileName,
        ReportFormat expectedFormat)
    {
        if (artifact.Format != expectedFormat || artifact.Bytes.Length == 0 ||
            artifact.Bytes.LongLength > MaximumOutputBytes ||
            !string.Equals(artifact.FileName, expectedFileName, StringComparison.Ordinal) ||
            !string.Equals(artifact.ContentType, manifest.ContentType, StringComparison.Ordinal) ||
            !string.Equals(artifact.ManifestSha256, manifest.Sha256, StringComparison.Ordinal) ||
            !string.Equals(artifact.VerificationCode, manifest.VerificationCode, StringComparison.Ordinal) ||
            !string.Equals(artifact.Sha256, ReportArtifactIdentity.Sha256(artifact.Bytes), StringComparison.Ordinal))
        {
            throw new ReportProcessingException(
                "reporting.output.integrity_failed",
                transient: false,
                permissionSnapshotJson: null);
        }
    }

    private static ReportFormat[] DeserializeFormats(string json)
    {
        var formats = JsonSerializer.Deserialize<ReportFormat[]>(json, CanonicalJson.SerializerOptions);
        if (formats is null || formats.Length is < 1 or > 2 ||
            formats.Distinct().Count() != formats.Length ||
            formats.Any(format => format is not (ReportFormat.Pdf or ReportFormat.Xlsx)))
        {
            throw new ReportProcessingException(
                "reporting.format.unsupported",
                transient: false,
                permissionSnapshotJson: null);
        }
        return formats.OrderBy(format => format).ToArray();
    }

    private static DocumentClassification ToDocumentClassification(ReportClassification classification) =>
        classification switch
        {
            ReportClassification.Internal => DocumentClassification.Internal,
            ReportClassification.Confidential => DocumentClassification.Confidential,
            ReportClassification.Restricted => DocumentClassification.Restricted,
            _ => throw new ReportProcessingException(
                "reporting.output.classification_invalid",
                transient: false,
                permissionSnapshotJson: null)
        };

    private static async Task<ClaimedRun?> ReadClaimAsync(
        NpgsqlCommand select,
        ReportWorkKind kind,
        bool incrementAttempt,
        CancellationToken cancellationToken)
    {
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? CreateClaim(reader, kind, incrementAttempt)
            : null;
    }

    private static ClaimedRun CreateClaim(
        NpgsqlDataReader reader,
        ReportWorkKind kind,
        bool incrementAttempt) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetGuid(2),
        reader.GetGuid(3),
        reader.GetString(4),
        reader.GetFieldValue<DateTimeOffset>(5),
        reader.GetInt32(6) + (incrementAttempt ? 1 : 0),
        reader.GetString(7),
        kind);

    private sealed record ClaimedRun(
        Guid Id,
        Guid TenantId,
        Guid ProjectId,
        Guid RequestedBy,
        string ParametersJson,
        DateTimeOffset AsOfUtc,
        int AttemptCount,
        string CorrelationId,
        ReportWorkKind WorkKind);

    private sealed record PreparedArtifact(
        Guid OutputId,
        Guid DocumentId,
        RenderedReportArtifact Artifact);

    private enum ReportWorkKind
    {
        Snapshot = 1,
        Rendering = 2
    }

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
        Message = "Reporting generation worker is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 6101,
        Level = LogLevel.Error,
        Message = "Reporting generation worker loop failed.")]
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

    [LoggerMessage(
        EventId = 6105,
        Level = LogLevel.Information,
        Message = "Reporting run {RunId} completed for project {ProjectId}.")]
    private static partial void LogRunCompleted(ILogger logger, Guid runId, Guid projectId);
}
