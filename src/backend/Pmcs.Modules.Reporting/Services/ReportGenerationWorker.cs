using System.Diagnostics;
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
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.ProjectIntelligence.Contracts;
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
    ReportingExecutionOptions execution,
    ReportingWorkerQualificationOptions qualification,
    ReportingWorkerTelemetry telemetry,
    ILogger<ReportGenerationWorker> logger) : BackgroundService
{
    private static readonly Guid SystemActorId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    private TimeSpan ProcessingLease => execution.ProcessingTimeout + TimeSpan.FromSeconds(30);

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
                telemetry.Heartbeat(DateTimeOffset.UtcNow);
                while (await ProcessNextAsync(stoppingToken))
                {
                    // Drain all currently eligible runs before waiting for the next poll.
                    telemetry.Heartbeat(DateTimeOffset.UtcNow);
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
            var now = clock.UtcNow;
            telemetry.Heartbeat(now);
            if (await FinalizeExhaustedAsync(
                    dbContext,
                    claimScope.ServiceProvider.GetRequiredService<ITransactionalSideEffectWriter>(),
                    now,
                    cancellationToken))
            {
                return true;
            }
            claimed = await ClaimNextSnapshotAsync(dbContext, now, cancellationToken)
                ?? await ClaimNextRenderingAsync(dbContext, now, cancellationToken);
        }

        if (claimed is null)
        {
            return false;
        }

        var workKind = claimed.WorkKind == ReportWorkKind.Snapshot ? "snapshot" : "rendering";
        var startedAt = Stopwatch.GetTimestamp();
        telemetry.RecordClaim(
            workKind,
            claimed.AttemptCount > 1,
            DateTimeOffset.UtcNow - claimed.CreatedAt);
        using var processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processingCancellation.CancelAfter(execution.ProcessingTimeout);
        var processingToken = processingCancellation.Token;
        var snapshotBuilt = claimed.WorkKind == ReportWorkKind.Rendering;
        try
        {
            if (claimed.WorkKind == ReportWorkKind.Snapshot)
            {
                await BuildSnapshotAsync(claimed, processingToken);
                snapshotBuilt = true;
                LogSnapshotBuilt(logger, claimed.Id, claimed.ProjectId);
            }

            var outputSizeBytes = await RenderOutputsAsync(claimed, processingToken);
            telemetry.RecordCompletion(
                workKind,
                Stopwatch.GetElapsedTime(startedAt),
                outputSizeBytes);
            LogRunCompleted(logger, claimed.Id, claimed.ProjectId);
        }
        catch (ReportProcessingException exception)
        {
            var willRetry = await RecordFailureAsync(
                claimed,
                exception.Code,
                exception.Transient,
                exception.PermissionSnapshotJson,
                cancellationToken);
            telemetry.RecordFailure(
                workKind,
                exception.Code,
                willRetry,
                Stopwatch.GetElapsedTime(startedAt));
            LogRunFailed(logger, claimed.Id, exception.Code);
        }
        catch (GeneratedDocumentPublishException exception)
        {
            var willRetry = await RecordFailureAsync(
                claimed,
                exception.Code,
                exception.Transient,
                permissionSnapshotJson: null,
                cancellationToken);
            telemetry.RecordFailure(
                workKind,
                exception.Code,
                willRetry,
                Stopwatch.GetElapsedTime(startedAt));
            LogRunFailed(logger, claimed.Id, exception.Code);
        }
        catch (ReportRenderingException exception)
        {
            var willRetry = await RecordFailureAsync(
                claimed,
                exception.Code,
                exception.Transient,
                permissionSnapshotJson: null,
                cancellationToken);
            telemetry.RecordFailure(
                workKind,
                exception.Code,
                willRetry,
                Stopwatch.GetElapsedTime(startedAt));
            LogRunFailed(logger, claimed.Id, exception.Code);
        }
        catch (DomainRuleException exception)
        {
            var willRetry = await RecordFailureAsync(
                claimed,
                exception.Code,
                transient: false,
                permissionSnapshotJson: null,
                cancellationToken: cancellationToken);
            telemetry.RecordFailure(
                workKind,
                exception.Code,
                willRetry,
                Stopwatch.GetElapsedTime(startedAt));
            LogRunFailed(logger, claimed.Id, exception.Code);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested && processingCancellation.IsCancellationRequested)
        {
            const string code = "reporting.run.timeout";
            var willRetry = await RecordFailureAsync(
                claimed,
                code,
                transient: true,
                permissionSnapshotJson: null,
                cancellationToken: cancellationToken);
            telemetry.RecordFailure(
                workKind,
                code,
                willRetry,
                Stopwatch.GetElapsedTime(startedAt));
            LogRunFailed(logger, claimed.Id, code);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var code = snapshotBuilt
                ? "reporting.renderer.transient"
                : "reporting.snapshot.transient";
            var willRetry = await RecordFailureAsync(
                claimed,
                code,
                transient: true,
                permissionSnapshotJson: null,
                cancellationToken: cancellationToken);
            telemetry.RecordFailure(
                workKind,
                code,
                willRetry,
                Stopwatch.GetElapsedTime(startedAt));
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
        var dbContext = services.GetRequiredService<ReportingDbContext>();
        var clock = services.GetRequiredService<IClock>();
        var sideEffectWriter = services.GetRequiredService<ITransactionalSideEffectWriter>();

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
                permissionSnapshotJson: null);
        }
        var permissionSnapshotJson = await RequireProcessingPermissionsAsync(
            permissionService,
            claimed,
            run.DefinitionCode,
            cancellationToken);
        run.RecordProcessingPermissionSnapshot(permissionSnapshotJson);
        await dbContext.SaveChangesAsync(cancellationToken);

        var project = await RequireActiveProjectAsync(
            projectDirectory,
            claimed,
            permissionSnapshotJson,
            cancellationToken);
        var builtAt = clock.UtcNow;
        ReportSnapshot snapshot;
        if (string.Equals(
                run.DefinitionCode,
                ReportDefinitionRuntimePolicy.DailyDefinitionCode,
                StringComparison.Ordinal))
        {
            if (run.PinnedProjectProfileJson is not null)
            {
                throw new ReportProcessingException(
                    "reporting.project_profile.invalid",
                    transient: false,
                    permissionSnapshotJson: permissionSnapshotJson);
            }

            var parameters = DeserializeStored<DailyReportReportParameters>(
                run.ParametersJson,
                "reporting.parameters.invalid",
                permissionSnapshotJson);
            var chain = await services.GetRequiredService<IDailyReportReportingSource>().LoadChainAsync(
                run.TenantId,
                run.ProjectId,
                parameters.DailyReportId,
                run.AsOfUtc,
                cancellationToken);
            snapshot = DailyReportSnapshotBuilder.Build(
                run.Id,
                run.TenantId,
                project,
                run.AsOfUtc,
                parameters,
                chain,
                builtAt);
        }
        else if (string.Equals(
                     run.DefinitionCode,
                     ProjectPeriodicReportRuntimeContract.DefinitionCode,
                     StringComparison.Ordinal))
        {
            var parameters = DeserializeStored<ProjectPeriodicReportParameters>(
                run.ParametersJson,
                "reporting.parameters.invalid",
                permissionSnapshotJson);
            var pinnedProject = DeserializeStored<ProjectPeriodicPinnedProjectProfile>(
                run.PinnedProjectProfileJson,
                "reporting.period.project_profile.invalid",
                permissionSnapshotJson);
            pinnedProject.ValidateForRun(
                run.TenantId,
                run.ProjectId,
                run.ProjectTimeZone,
                run.CreatedAt);
            var period = ProjectPeriodicReportPeriodResolver.Resolve(
                parameters,
                pinnedProject.TimeZone,
                run.AsOfUtc,
                run.CreatedAt);
            var source = await services.GetRequiredService<IDailyReportPeriodReportingSource>().LoadPeriodAsync(
                run.TenantId,
                run.ProjectId,
                period.PeriodStartLocalDate,
                period.PeriodEndLocalDateExclusive,
                period.SourceCutoffUtc,
                cancellationToken);
            snapshot = ProjectPeriodicReportSnapshotBuilder.Build(
                run.Id,
                run.TenantId,
                pinnedProject,
                run.AsOfUtc,
                parameters,
                source,
                run.CreatedAt,
                builtAt);
        }
        else if (string.Equals(
                     run.DefinitionCode,
                     ExecutiveProjectStateReportRuntimeContract.DefinitionCode,
                     StringComparison.Ordinal))
        {
            _ = DeserializeStored<ExecutiveProjectStateReportParameters>(
                run.ParametersJson,
                "reporting.parameters.invalid",
                permissionSnapshotJson);
            var pinnedProject = DeserializeStored<ExecutiveProjectStatePinnedProjectProfile>(
                run.PinnedProjectProfileJson,
                "reporting.executive_state.project_scope.invalid",
                permissionSnapshotJson);
            var timeZone = pinnedProject.ValidateForRun(
                run.TenantId,
                run.ProjectId,
                run.AsOfUtc,
                run.CreatedAt);
            var cutoffLocalDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(run.AsOfUtc, timeZone).DateTime);
            var source = await services.GetRequiredService<IProjectStateReportingSource>().LoadAsync(
                run.TenantId,
                run.ProjectId,
                cutoffLocalDate,
                run.AsOfUtc,
                cancellationToken);
            snapshot = ExecutiveProjectStateReportSnapshotBuilder.Build(
                run.Id,
                run.TenantId,
                pinnedProject,
                run.AsOfUtc,
                source,
                run.CreatedAt,
                builtAt);
        }
        else if (string.Equals(
                     run.DefinitionCode,
                     ProjectProgressReportRuntimeContract.DefinitionCode,
                     StringComparison.Ordinal))
        {
            _ = DeserializeStored<ProjectProgressReportParameters>(
                run.ParametersJson,
                "reporting.parameters.invalid",
                permissionSnapshotJson);
            var pinnedProject = DeserializeStored<ProjectProgressPinnedProjectProfile>(
                run.PinnedProjectProfileJson,
                "reporting.project_progress.project_scope.invalid",
                permissionSnapshotJson);
            var timeZone = pinnedProject.ValidateForRun(
                run.TenantId,
                run.ProjectId,
                run.AsOfUtc,
                run.CreatedAt);
            var cutoffLocalDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(run.AsOfUtc, timeZone).DateTime);
            var source = await services.GetRequiredService<IProjectProgressReportingSource>().LoadAsync(
                run.TenantId,
                run.ProjectId,
                cutoffLocalDate,
                run.AsOfUtc,
                cancellationToken);
            snapshot = ProjectProgressReportSnapshotBuilder.Build(
                run.Id,
                run.TenantId,
                pinnedProject,
                run.AsOfUtc,
                source,
                run.CreatedAt,
                builtAt);
        }
        else
        {
            throw new ReportProcessingException(
                "reporting.definition.invalid",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }

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
                    ["workerInstanceId"] = qualification.WorkerInstanceId,
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

    private async Task<long> RenderOutputsAsync(ClaimedRun claimed, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var permissionService = services.GetRequiredService<IProjectPermissionService>();
        var projectDirectory = services.GetRequiredService<IProjectDirectory>();
        var dbContext = services.GetRequiredService<ReportingDbContext>();
        var rendererRegistry = services.GetRequiredService<ReportRendererRegistry>();
        var periodicRendererRegistry = services.GetRequiredService<ProjectPeriodicReportRendererRegistry>();
        var executiveRendererRegistry =
            services.GetRequiredService<ExecutiveProjectStateReportRendererRegistry>();
        var progressRendererRegistry = services.GetRequiredService<ProjectProgressReportRendererRegistry>();
        var publisher = services.GetRequiredService<IGeneratedDocumentPublisher>();
        var sideEffectWriter = services.GetRequiredService<ITransactionalSideEffectWriter>();
        var clock = services.GetRequiredService<IClock>();

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
                permissionSnapshotJson: null);
        }
        var permissionSnapshotJson = await RequireProcessingPermissionsAsync(
            permissionService,
            claimed,
            run.DefinitionCode,
            cancellationToken);
        _ = await RequireActiveProjectAsync(
            projectDirectory,
            claimed,
            permissionSnapshotJson,
            cancellationToken);

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
        DailyReportRenderSnapshot? dailyRenderSnapshot = null;
        ProjectPeriodicReportSemanticSnapshot? periodicRenderSnapshot = null;
        ExecutiveProjectStateReportSemanticSnapshot? executiveRenderSnapshot = null;
        ProjectProgressReportSemanticSnapshot? progressRenderSnapshot = null;
        if (string.Equals(
                run.DefinitionCode,
                ReportDefinitionRuntimePolicy.DailyDefinitionCode,
                StringComparison.Ordinal))
        {
            dailyRenderSnapshot = DailyReportRenderSnapshot.Parse(snapshot.PayloadJson);
            VerifyRenderSnapshot(dailyRenderSnapshot, snapshot, run);
        }
        else if (string.Equals(
                     run.DefinitionCode,
                     ProjectPeriodicReportRuntimeContract.DefinitionCode,
                     StringComparison.Ordinal))
        {
            periodicRenderSnapshot = ProjectPeriodicReportRenderSnapshot.Parse(snapshot.PayloadJson);
            VerifyRenderSnapshot(periodicRenderSnapshot, snapshot, run);
        }
        else if (string.Equals(
                     run.DefinitionCode,
                     ExecutiveProjectStateReportRuntimeContract.DefinitionCode,
                     StringComparison.Ordinal))
        {
            executiveRenderSnapshot = ExecutiveProjectStateReportRenderSnapshot.Parse(snapshot.PayloadJson);
            VerifyRenderSnapshot(executiveRenderSnapshot, snapshot, run);
        }
        else if (string.Equals(
                     run.DefinitionCode,
                     ProjectProgressReportRuntimeContract.DefinitionCode,
                     StringComparison.Ordinal))
        {
            progressRenderSnapshot = ProjectProgressReportRenderSnapshot.Parse(snapshot.PayloadJson);
            VerifyRenderSnapshot(progressRenderSnapshot, snapshot, run);
        }
        else
        {
            throw new ReportProcessingException(
                "reporting.definition.invalid",
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }
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
                        ["workerInstanceId"] = qualification.WorkerInstanceId,
                        ["requestedBy"] = run.RequestedBy,
                        ["snapshotId"] = snapshot.Id,
                        ["snapshotHash"] = snapshot.Sha256,
                        ["attempt"] = run.AttemptCount
                    },
                    run.CorrelationId),
                cancellationToken);
            await startTransaction.CommitAsync(cancellationToken);
        }

        await PauseForQualificationAsync(
            ReportingWorkerQualificationPausePoint.BeforeStorage,
            run.Id,
            cancellationToken);
        if (qualification.ShouldFail(
                ReportingWorkerQualificationFailurePoint.BeforeStorageTransientFailure,
                run.Id))
        {
            throw new ReportProcessingException(
                "reporting.qa.transient_injected",
                transient: true,
                permissionSnapshotJson: permissionSnapshotJson);
        }

        var formats = DeserializeFormats(run.RequestedFormatsJson);
        var rendered = new List<PreparedArtifact>(formats.Length);
        foreach (var format in formats)
        {
            var outputId = ReportArtifactIdentity.OutputId(run.Id, format);
            var documentId = ReportArtifactIdentity.DocumentId(outputId);
            var manifest = ReportArtifactIdentity.CreateManifest(run, snapshot, template, format);
            var fileName = dailyRenderSnapshot is not null
                ? ReportArtifactIdentity.FileName(dailyRenderSnapshot, format)
                : periodicRenderSnapshot is not null
                    ? ReportArtifactIdentity.FileName(periodicRenderSnapshot, format)
                    : executiveRenderSnapshot is not null
                        ? ReportArtifactIdentity.FileName(executiveRenderSnapshot, format)
                        : ReportArtifactIdentity.FileName(progressRenderSnapshot!, format);
            RenderedReportArtifact artifact;
            if (dailyRenderSnapshot is not null)
            {
                artifact = rendererRegistry.Require(format).Render(new ReportRenderRequest(
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
                    dailyRenderSnapshot));
            }
            else if (periodicRenderSnapshot is not null)
            {
                artifact = periodicRendererRegistry.Require(format).Render(
                    new ProjectPeriodicReportRenderRequest(
                        run.Id,
                        outputId,
                        snapshot.Id,
                        template.Id,
                        run.DefinitionCode,
                        ProjectPeriodicReportRuntimeContract.DefinitionVersion,
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
                        periodicRenderSnapshot!));
            }
            else if (executiveRenderSnapshot is not null)
            {
                artifact = executiveRendererRegistry.Require(format).Render(
                    new ExecutiveProjectStateReportRenderRequest(
                        run.Id,
                        outputId,
                        snapshot.Id,
                        template.Id,
                        run.DefinitionCode,
                        ExecutiveProjectStateReportRuntimeContract.DefinitionVersion,
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
                        executiveRenderSnapshot));
            }
            else
            {
                artifact = progressRendererRegistry.Require(format).Render(
                    new ProjectProgressReportRenderRequest(
                        run.Id,
                        outputId,
                        snapshot.Id,
                        template.Id,
                        run.DefinitionCode,
                        ProjectProgressReportRuntimeContract.DefinitionVersion,
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
                        progressRenderSnapshot!));
            }
            VerifyRenderedArtifact(artifact, manifest, fileName, format);
            rendered.Add(new PreparedArtifact(outputId, documentId, artifact));
        }

        await PauseForQualificationAsync(
            ReportingWorkerQualificationPausePoint.BeforeStoragePermissionRecheck,
            run.Id,
            cancellationToken);
        permissionSnapshotJson = await RequireProcessingPermissionsAsync(
            permissionService,
            claimed,
            run.DefinitionCode,
            cancellationToken);
        run.RecordRenderingPermissionSnapshot(permissionSnapshotJson);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        await PauseForQualificationAsync(
            ReportingWorkerQualificationPausePoint.AfterStorage,
            run.Id,
            cancellationToken);

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
                        ["workerInstanceId"] = qualification.WorkerInstanceId,
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
        return rendered.Sum(item => item.Artifact.Bytes.LongLength);
    }

    private async Task<bool> FinalizeExhaustedAsync(
        ReportingDbContext dbContext,
        ITransactionalSideEffectWriter sideEffectWriter,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var select = new NpgsqlCommand(
            """
            select candidate.id
            from reporting.report_runs candidate
            where candidate.attempt_count >= @maximum_attempts
              and candidate.output_count = 0
              and (
                  (candidate.status = 'Queued' and
                      (candidate.next_attempt_at is null or candidate.next_attempt_at <= @now))
                  or
                  (candidate.status = 'Processing' and
                      candidate.pipeline_stage = 'SnapshotReady' and
                      candidate.diagnostic_code is not null and
                      (candidate.next_attempt_at is null or candidate.next_attempt_at <= @now))
                  or
                  (candidate.status = 'Processing' and
                      candidate.pipeline_stage in ('BuildingSnapshot', 'Rendering') and
                      candidate.claimed_at < @lease_cutoff)
              )
            order by candidate.created_at, candidate.id
            for update of candidate skip locked
            limit 1;
            """,
            connection,
            postgresTransaction);
        select.Parameters.AddWithValue("maximum_attempts", execution.MaximumAttempts);
        select.Parameters.AddWithValue("now", now);
        select.Parameters.AddWithValue("lease_cutoff", now.Subtract(ProcessingLease));
        var selected = await select.ExecuteScalarAsync(cancellationToken);
        if (selected is not Guid runId)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var run = await dbContext.Runs.SingleAsync(item => item.Id == runId, cancellationToken);
        var previousDiagnosticCode = run.DiagnosticCode;
        var previousStage = run.PipelineStage;
        var workKind = run.SnapshotId.HasValue ? "rendering" : "snapshot";
        const string diagnosticCode = "reporting.retry.exhausted";
        run.Fail(diagnosticCode, diagnosticDetail: null, failedAt: now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAuditAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new AuditEntry(
                run.TenantId,
                run.ProjectId,
                SystemActorId,
                "CertifiedReportRunFailed",
                "ReportRun",
                run.Id.ToString(),
                now,
                new Dictionary<string, object?>
                {
                    ["executor"] = "SystemWorker",
                    ["workerInstanceId"] = qualification.WorkerInstanceId,
                    ["requestedBy"] = run.RequestedBy,
                    ["diagnosticCode"] = diagnosticCode,
                    ["previousDiagnosticCode"] = previousDiagnosticCode,
                    ["attempt"] = run.AttemptCount,
                    ["willRetry"] = false,
                    ["stage"] = previousStage.ToString()
                },
                run.CorrelationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        telemetry.RecordFailure(
            workKind,
            diagnosticCode,
            willRetry: false,
            duration: TimeSpan.Zero,
            activeClaim: false);
        LogRunFailed(logger, run.Id, diagnosticCode);
        return true;
    }

    private async Task<ClaimedRun?> ClaimNextSnapshotAsync(
        ReportingDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var select = new NpgsqlCommand(
            """
            select candidate.id, candidate.tenant_id, candidate.project_id,
                   candidate.requested_by, candidate.parameters_json::text,
                   candidate.as_of_utc, candidate.attempt_count,
                   candidate.correlation_id, candidate.created_at
            from reporting.report_runs candidate
            where candidate.snapshot_id is null
              and candidate.attempt_count < @maximum_attempts
              and (
                  (candidate.status = 'Queued' and
                      (candidate.next_attempt_at is null or candidate.next_attempt_at <= @now))
                  or
                  (candidate.status = 'Processing' and
                      candidate.pipeline_stage = 'BuildingSnapshot' and
                      candidate.claimed_at < @lease_cutoff)
              )
            order by (
                select count(*)
                from reporting.report_runs peer
                where peer.tenant_id = candidate.tenant_id
                  and peer.project_id = candidate.project_id
                  and peer.snapshot_id is null
                  and peer.attempt_count < @maximum_attempts
                  and (
                      (peer.status = 'Queued' and
                          (peer.next_attempt_at is null or peer.next_attempt_at <= @now))
                      or
                      (peer.status = 'Processing' and
                          peer.pipeline_stage = 'BuildingSnapshot' and
                          peer.claimed_at < @lease_cutoff)
                  )
                  and (peer.created_at, peer.id) < (candidate.created_at, candidate.id)
            ), coalesce((
                select max(history.claimed_at)
                from reporting.report_runs history
                where history.tenant_id = candidate.tenant_id
                  and history.project_id = candidate.project_id
            ), '-infinity'::timestamptz), candidate.created_at, candidate.id
            for update of candidate skip locked
            limit 1;
            """,
            connection,
            postgresTransaction);
        select.Parameters.AddWithValue("maximum_attempts", execution.MaximumAttempts);
        select.Parameters.AddWithValue("now", now);
        select.Parameters.AddWithValue("lease_cutoff", now.Subtract(ProcessingLease));

        var claimed = await ReadClaimAsync(select, ReportWorkKind.Snapshot, incrementAttempt: true, cancellationToken);
        if (claimed is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await PauseForQualificationAsync(
            ReportingWorkerQualificationPausePoint.AfterSnapshotRowLock,
            claimed.Id,
            cancellationToken);

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

    private async Task PauseForQualificationAsync(
        ReportingWorkerQualificationPausePoint point,
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (!qualification.ShouldPause(point, runId))
        {
            return;
        }

        LogQualificationPause(logger, qualification.WorkerInstanceId, point, runId);
        await Task.Delay(
            qualification.PauseDuration ?? Timeout.InfiniteTimeSpan,
            cancellationToken);
    }

    private async Task<ClaimedRun?> ClaimNextRenderingAsync(
        ReportingDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var postgresTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await using var select = new NpgsqlCommand(
            """
            select candidate.id, candidate.tenant_id, candidate.project_id,
                   candidate.requested_by, candidate.parameters_json::text,
                   candidate.as_of_utc, candidate.attempt_count,
                   candidate.correlation_id, candidate.created_at,
                   (candidate.pipeline_stage = 'Rendering' or
                       candidate.diagnostic_code is not null) as retry_attempt
            from reporting.report_runs candidate
            where candidate.status = 'Processing'
              and candidate.snapshot_id is not null
              and candidate.output_count = 0
              and (
                  (candidate.pipeline_stage = 'SnapshotReady'
                      and (candidate.next_attempt_at is null or candidate.next_attempt_at <= @now)
                      and (candidate.diagnostic_code is null or
                          candidate.attempt_count < @maximum_attempts))
                  or
                  (candidate.pipeline_stage = 'Rendering'
                      and candidate.claimed_at < @lease_cutoff
                      and candidate.attempt_count < @maximum_attempts)
              )
            order by (
                select count(*)
                from reporting.report_runs peer
                where peer.tenant_id = candidate.tenant_id
                  and peer.project_id = candidate.project_id
                  and peer.status = 'Processing'
                  and peer.snapshot_id is not null
                  and peer.output_count = 0
                  and (
                      (peer.pipeline_stage = 'SnapshotReady'
                          and (peer.next_attempt_at is null or peer.next_attempt_at <= @now)
                          and (peer.diagnostic_code is null or
                              peer.attempt_count < @maximum_attempts))
                      or
                      (peer.pipeline_stage = 'Rendering'
                          and peer.claimed_at < @lease_cutoff
                          and peer.attempt_count < @maximum_attempts)
                  )
                  and (peer.created_at, peer.id) < (candidate.created_at, candidate.id)
            ), coalesce((
                select max(history.claimed_at)
                from reporting.report_runs history
                where history.tenant_id = candidate.tenant_id
                  and history.project_id = candidate.project_id
            ), '-infinity'::timestamptz), candidate.created_at, candidate.id
            for update of candidate skip locked
            limit 1;
            """,
            connection,
            postgresTransaction);
        select.Parameters.AddWithValue("maximum_attempts", execution.MaximumAttempts);
        select.Parameters.AddWithValue("now", now);
        select.Parameters.AddWithValue("lease_cutoff", now.Subtract(ProcessingLease));

        ClaimedRun? claimed = null;
        var retryAttempt = false;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                retryAttempt = reader.GetBoolean(9);
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

    private async Task<bool> RecordFailureAsync(
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
            return false;
        }

        var now = clock.UtcNow;
        if (transient && run.AttemptCount < execution.MaximumAttempts)
        {
            var retryAt = now.Add(execution.RetryBaseDelay * Math.Max(1, run.AttemptCount));
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
                    ["workerInstanceId"] = qualification.WorkerInstanceId,
                    ["requestedBy"] = claimed.RequestedBy,
                    ["diagnosticCode"] = code,
                    ["attempt"] = run.AttemptCount,
                    ["willRetry"] = run.Status is ReportRunStatus.Queued or ReportRunStatus.Processing,
                    ["stage"] = run.PipelineStage.ToString()
                },
                claimed.CorrelationId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return run.Status is ReportRunStatus.Queued or ReportRunStatus.Processing;
    }

    private static async Task<string> RequireProcessingPermissionsAsync(
        IProjectPermissionService permissionService,
        ClaimedRun claimed,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        if (!ReportDefinitionRuntimePolicy.TryGetSourcePermissions(
                definitionCode,
                out var sourcePermissions))
        {
            throw new ReportProcessingException(
                "reporting.definition.invalid",
                transient: false,
                permissionSnapshotJson: null);
        }

        var preview = await permissionService.PreviewProjectPermissionsAsync(
            claimed.TenantId,
            claimed.RequestedBy,
            claimed.ProjectId,
            operations: ["reporting.run.create", .. sourcePermissions],
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

    private static void VerifyRenderSnapshot(
        ProjectPeriodicReportSemanticSnapshot renderSnapshot,
        ReportSnapshot snapshot,
        ReportRun run)
    {
        if (!string.Equals(renderSnapshot.SchemaVersion, snapshot.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(renderSnapshot.DefinitionCode, run.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(
                renderSnapshot.DefinitionVersion,
                ProjectPeriodicReportRuntimeContract.DefinitionVersion,
                StringComparison.Ordinal) ||
            renderSnapshot.Project.Id != run.ProjectId || renderSnapshot.Project.TenantId != run.TenantId ||
            renderSnapshot.DataStatus != snapshot.DataStatus ||
            renderSnapshot.Period.SourceCutoffUtc.ToUniversalTime() != run.AsOfUtc.ToUniversalTime() ||
            !string.Equals(
                renderSnapshot.SourceManifestSha256,
                snapshot.SourceManifestSha256,
                StringComparison.Ordinal))
        {
            throw new ReportProcessingException(
                "reporting.snapshot.integrity_failed",
                transient: false,
                permissionSnapshotJson: null);
        }
    }

    private static void VerifyRenderSnapshot(
        ExecutiveProjectStateReportSemanticSnapshot renderSnapshot,
        ReportSnapshot snapshot,
        ReportRun run)
    {
        if (!string.Equals(renderSnapshot.SchemaVersion, snapshot.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(renderSnapshot.DefinitionCode, run.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(
                renderSnapshot.DefinitionVersion,
                ExecutiveProjectStateReportRuntimeContract.DefinitionVersion,
                StringComparison.Ordinal) ||
            renderSnapshot.Project.Id != run.ProjectId || renderSnapshot.Project.TenantId != run.TenantId ||
            renderSnapshot.DataStatus != snapshot.DataStatus ||
            renderSnapshot.Cutoff.SourceCutoffUtc.ToUniversalTime() != run.AsOfUtc.ToUniversalTime() ||
            !string.Equals(
                renderSnapshot.SourceManifestSha256,
                snapshot.SourceManifestSha256,
                StringComparison.Ordinal))
        {
            throw new ReportProcessingException(
                "reporting.snapshot.integrity_failed",
                transient: false,
                permissionSnapshotJson: null);
        }
    }

    private static void VerifyRenderSnapshot(
        ProjectProgressReportSemanticSnapshot renderSnapshot,
        ReportSnapshot snapshot,
        ReportRun run)
    {
        if (!string.Equals(renderSnapshot.SchemaVersion, snapshot.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(renderSnapshot.DefinitionCode, run.DefinitionCode, StringComparison.Ordinal) ||
            !string.Equals(
                renderSnapshot.DefinitionVersion,
                ProjectProgressReportRuntimeContract.DefinitionVersion,
                StringComparison.Ordinal) ||
            renderSnapshot.Project.Id != run.ProjectId || renderSnapshot.Project.TenantId != run.TenantId ||
            renderSnapshot.DataStatus != snapshot.DataStatus ||
            renderSnapshot.Cutoff.SourceCutoffUtc.ToUniversalTime() != run.AsOfUtc.ToUniversalTime() ||
            !string.Equals(
                renderSnapshot.SourceManifestSha256,
                snapshot.SourceManifestSha256,
                StringComparison.Ordinal))
        {
            throw new ReportProcessingException(
                "reporting.snapshot.integrity_failed",
                transient: false,
                permissionSnapshotJson: null);
        }
    }

    private static T DeserializeStored<T>(
        string? json,
        string diagnosticCode,
        string? permissionSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ReportProcessingException(
                diagnosticCode,
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, CanonicalJson.SerializerOptions)
                ?? throw new ReportProcessingException(
                    diagnosticCode,
                    transient: false,
                    permissionSnapshotJson: permissionSnapshotJson);
        }
        catch (JsonException)
        {
            throw new ReportProcessingException(
                diagnosticCode,
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }
        catch (NotSupportedException)
        {
            throw new ReportProcessingException(
                diagnosticCode,
                transient: false,
                permissionSnapshotJson: permissionSnapshotJson);
        }
    }

    private void VerifyRenderedArtifact(
        RenderedReportArtifact artifact,
        ReportArtifactManifest manifest,
        string expectedFileName,
        ReportFormat expectedFormat)
    {
        if (artifact.Format != expectedFormat || artifact.Bytes.Length == 0 ||
            artifact.Bytes.LongLength > execution.MaximumOutputBytes ||
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
        reader.GetFieldValue<DateTimeOffset>(8),
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
        DateTimeOffset CreatedAt,
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

    [LoggerMessage(
        EventId = 6106,
        Level = LogLevel.Warning,
        Message = "Reporting QA worker {WorkerInstanceId} paused at {PausePoint} for run {RunId}.")]
    private static partial void LogQualificationPause(
        ILogger logger,
        string workerInstanceId,
        ReportingWorkerQualificationPausePoint pausePoint,
        Guid runId);
}
