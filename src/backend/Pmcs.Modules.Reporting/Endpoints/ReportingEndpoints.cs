using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Documents.Contracts;
using Pmcs.Modules.Documents.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Modules.Reporting.Endpoints;

internal static class ReportingEndpoints
{
    private const string DefinitionCode = "daily-report-certified";
    private const string SourcePermission = "field.daily-reports.read";
    private const string CreateOperation = "reporting.run.create";
    private static readonly HashSet<string> DailyParameterNames =
        new(StringComparer.Ordinal) { "dailyReportId", "includeRevisionChain" };

    public static void MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/reports")
            .WithTags("Reporting Center");
        group.MapGet("/catalog", ListCatalogAsync);
        group.MapGet("/catalog/{definitionCode}", GetCatalogDefinitionAsync);
        group.MapPost("/runs", CreateRunAsync);
        group.MapGet("/runs", ListRunsAsync);
        group.MapGet("/runs/{runId:guid}", GetRunAsync);
        group.MapPost("/runs/{runId:guid}/retry", RetryRunAsync);
        group.MapPost("/runs/{runId:guid}/cancel", CancelRunAsync);
        group.MapGet("/outputs/{outputId:guid}/content", DownloadOutputAsync);
        group.MapGet("/outputs/{outputId:guid}/verify", VerifyOutputAsync);
    }

    private static async Task<IResult> ListCatalogAsync(
        Guid projectId,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = await GateCatalogAsync(
            projectId,
            runtime,
            actor,
            permissionService,
            projectDirectory,
            cancellationToken);
        if (gate.Result is not null)
        {
            return gate.Result;
        }

        if (!gate.SourceAllowed)
        {
            return Results.Ok(Array.Empty<ReportCatalogDefinitionResponse>());
        }

        var definitions = await dbContext.Definitions.AsNoTracking()
            .Where(item => item.Status == ReportDefinitionStatus.Active && item.Code == DefinitionCode)
            .OrderBy(item => item.Code)
            .ToArrayAsync(cancellationToken);
        var responses = new List<ReportCatalogDefinitionResponse>(definitions.Length);
        foreach (var definition in definitions)
        {
            responses.Add(await ToCatalogResponseAsync(definition, dbContext, cancellationToken));
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> GetCatalogDefinitionAsync(
        Guid projectId,
        string definitionCode,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = await GateCatalogAsync(
            projectId,
            runtime,
            actor,
            permissionService,
            projectDirectory,
            cancellationToken);
        if (gate.Result is not null)
        {
            return gate.Result;
        }

        if (!gate.SourceAllowed || !string.Equals(definitionCode, DefinitionCode, StringComparison.Ordinal))
        {
            return Results.NotFound(new { code = "reporting.definition.not_found" });
        }

        var definition = await dbContext.Definitions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Code == definitionCode && item.Status == ReportDefinitionStatus.Active,
            cancellationToken);
        return definition is null
            ? Results.NotFound(new { code = "reporting.definition.not_found" })
            : Results.Ok(await ToCatalogResponseAsync(definition, dbContext, cancellationToken));
    }

    private static async Task<IResult> CreateRunAsync(
        Guid projectId,
        CreateReportRunRequest request,
        HttpContext httpContext,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        ReportingDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!runtime.Phase1Enabled)
        {
            return Results.NotFound();
        }

        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                CreateOperation,
                cancellationToken))
        {
            return Problem(StatusCodes.Status403Forbidden, "reporting.permission.denied", "Report creation is not permitted.");
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (project.Status != ProjectStatus.Active)
        {
            return Problem(StatusCodes.Status409Conflict, "project.not_operational", "Project is not active.");
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                SourcePermission,
                cancellationToken))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "reporting.source_permission.denied",
                "The report source is not permitted.");
        }

        if (request.ClientGeneratedId == Guid.Empty)
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.run.identity.invalid", "Client-generated run id is required.");
        }

        var definitionCode = request.DefinitionCode?.Trim() ?? string.Empty;
        if (!string.Equals(definitionCode, DefinitionCode, StringComparison.Ordinal))
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.definition.invalid", "Report definition is invalid.");
        }

        var definition = await dbContext.Definitions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Code == definitionCode && item.Status == ReportDefinitionStatus.Active,
            cancellationToken);
        if (definition is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.definition.invalid", "Report definition is invalid.");
        }

        var templateVersion = request.TemplateVersion?.Trim() ?? string.Empty;
        var template = await dbContext.TemplateVersions.AsNoTracking().SingleOrDefaultAsync(
            item => item.DefinitionId == definition.Id &&
                item.Version == templateVersion &&
                !item.RetiredAt.HasValue,
            cancellationToken);
        if (template is null || template.Id != definition.CurrentTemplateVersionId)
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.template.invalid", "Report template is invalid.");
        }

        var formats = NormalizeFormats(request.Formats);
        if (formats is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.format.unsupported", "Requested format is unsupported.");
        }

        var parameters = ParseDailyParameters(request.Parameters);
        if (parameters is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.parameters.invalid", "Report parameters are invalid.");
        }

        var now = clock.UtcNow.ToUniversalTime();
        var asOfUtc = request.AsOfUtc?.ToUniversalTime() ?? now;
        if (asOfUtc > now)
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.as_of.future", "Report cutoff cannot be in the future.");
        }

        var permissionPreview = await permissionService.PreviewProjectPermissionsAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            operations: [CreateOperation, SourcePermission],
            cancellationToken: cancellationToken);
        if (permissionPreview.Decisions.Any(decision => !decision.Allowed))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "reporting.permission.denied",
                "Current permissions do not satisfy the report definition.");
        }

        var permissionSnapshotJson = SerializePermissionSnapshot(permissionPreview);
        var parametersJson = CanonicalJson.Serialize(parameters);
        var requestedFormatsJson = CanonicalJson.Serialize(formats);
        var canonicalRequest = CanonicalJson.Serialize(new CreateRunIdentity(
            projectId,
            request.ClientGeneratedId,
            definitionCode,
            templateVersion,
            asOfUtc,
            formats,
            parameters));
        var requestHash = RequestHash.Create(canonicalRequest);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        IdempotencyKeyRules.Validate(idempotencyKey);
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId,
            idempotencyKey,
            CreateOperation,
            requestHash,
            cancellationToken);
        if (replay is not null)
        {
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        }

        var run = ReportRun.Queue(
            request.ClientGeneratedId,
            actor.TenantId,
            projectId,
            definition.Id,
            definition.Code,
            template.Id,
            template.Version,
            parametersJson,
            CanonicalJson.Sha256(parametersJson),
            requestedFormatsJson,
            asOfUtc,
            project.TimeZone,
            actor.UserId,
            permissionSnapshotJson,
            httpContext.TraceIdentifier,
            CanonicalJson.Sha256(idempotencyKey),
            now);
        dbContext.Runs.Add(run);
        var response = ToRunResponse(run, null, []);
        var responseJson = JsonSerializer.Serialize(response, CanonicalJson.SerializerOptions);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    projectId,
                    actor.UserId,
                    "CertifiedReportRunQueued",
                    "ReportRun",
                    run.Id.ToString(),
                    now,
                    new Dictionary<string, object?>
                    {
                        ["definitionCode"] = run.DefinitionCode,
                        ["templateVersion"] = run.TemplateVersion,
                        ["asOfUtc"] = run.AsOfUtc,
                        ["parametersHash"] = run.ParametersHash,
                        ["formats"] = formats.Select(format => format.ToString()).ToArray()
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    "Reporting.ReportRunQueued",
                    1,
                    now,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotencyKey,
                    CreateOperation,
                    requestHash,
                    StatusCodes.Status202Accepted,
                    responseJson,
                    now,
                    now.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Accepted(response.Links.Self, response);
    }

    private static async Task<IResult> ListRunsAsync(
        Guid projectId,
        ReportRunStatus? status,
        string? definitionCode,
        int? limit,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = await GateRunReadAsync(projectId, runtime, actor, permissionService, cancellationToken);
        if (gate is not null)
        {
            return gate;
        }

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            return Problem(StatusCodes.Status400BadRequest, "reporting.status.invalid", "Run status is invalid.");
        }

        var take = Math.Clamp(limit ?? 50, 1, 100);
        var query = dbContext.Runs.AsNoTracking()
            .Where(run => run.TenantId == actor.TenantId && run.ProjectId == projectId);
        if (status.HasValue)
        {
            query = query.Where(run => run.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(definitionCode))
        {
            var normalizedCode = definitionCode.Trim();
            query = query.Where(run => run.DefinitionCode == normalizedCode);
        }

        var runs = await query
            .OrderByDescending(run => run.CreatedAt)
            .ThenByDescending(run => run.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        var responses = new List<ReportRunResponse>(runs.Length);
        foreach (var run in runs)
        {
            responses.Add(await LoadRunResponseAsync(run, dbContext, cancellationToken));
        }

        return Results.Ok(responses);
    }

    private static async Task<IResult> GetRunAsync(
        Guid projectId,
        Guid runId,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = await GateRunReadAsync(projectId, runtime, actor, permissionService, cancellationToken);
        if (gate is not null)
        {
            return gate;
        }

        var run = await dbContext.Runs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == runId && item.TenantId == actor.TenantId && item.ProjectId == projectId,
            cancellationToken);
        return run is null
            ? Results.NotFound(new { code = "reporting.run.not_found" })
            : Results.Ok(await LoadRunResponseAsync(run, dbContext, cancellationToken));
    }

    private static async Task<IResult> RetryRunAsync(
        Guid projectId,
        Guid runId,
        HttpContext httpContext,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        ReportingDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var gate = await GateRunMutationAsync(
            projectId,
            requireActiveProject: true,
            runtime,
            actor,
            permissionService,
            projectDirectory,
            cancellationToken);
        if (gate is not null)
        {
            return gate;
        }

        var run = await dbContext.Runs.SingleOrDefaultAsync(item =>
            item.Id == runId && item.TenantId == actor.TenantId && item.ProjectId == projectId,
            cancellationToken);
        if (run is null)
        {
            return Results.NotFound(new { code = "reporting.run.not_found" });
        }

        var idempotency = await FindMutationReplayAsync(
            httpContext,
            actor.TenantId,
            idempotencyStore,
            "reporting.run.retry",
            new { projectId, runId },
            cancellationToken);
        if (idempotency.Replay is not null)
        {
            return idempotency.Replay;
        }

        var previousDiagnostic = run.DiagnosticCode;
        if (run.Status != ReportRunStatus.Failed || run.OutputCount > 0 ||
            run.AttemptCount >= ReportGenerationWorker.MaximumAttempts ||
            !IsExplicitlyRetryable(previousDiagnostic))
        {
            return Problem(
                StatusCodes.Status409Conflict,
                "reporting.run.not_retryable",
                "Report run is not eligible for retry.");
        }

        var now = clock.UtcNow;
        run.RetryFailed(now);
        var response = await LoadRunResponseAsync(run, dbContext, cancellationToken);
        var responseJson = JsonSerializer.Serialize(response, CanonicalJson.SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    projectId,
                    actor.UserId,
                    "CertifiedReportRunRetried",
                    "ReportRun",
                    run.Id.ToString(),
                    now,
                    new Dictionary<string, object?>
                    {
                        ["previousDiagnosticCode"] = previousDiagnostic,
                        ["attempt"] = run.AttemptCount,
                        ["snapshotPreserved"] = run.SnapshotId.HasValue
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    "Reporting.ReportRunRetried",
                    1,
                    now,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    "reporting.run.retry",
                    idempotency.RequestHash,
                    StatusCodes.Status202Accepted,
                    responseJson,
                    now,
                    now.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Accepted(response.Links.Self, response);
    }

    private static async Task<IResult> CancelRunAsync(
        Guid projectId,
        Guid runId,
        HttpContext httpContext,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        ReportingDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var gate = await GateRunMutationAsync(
            projectId,
            requireActiveProject: false,
            runtime,
            actor,
            permissionService,
            projectDirectory,
            cancellationToken);
        if (gate is not null)
        {
            return gate;
        }

        var run = await dbContext.Runs.SingleOrDefaultAsync(item =>
            item.Id == runId && item.TenantId == actor.TenantId && item.ProjectId == projectId,
            cancellationToken);
        if (run is null)
        {
            return Results.NotFound(new { code = "reporting.run.not_found" });
        }

        var idempotency = await FindMutationReplayAsync(
            httpContext,
            actor.TenantId,
            idempotencyStore,
            "reporting.run.cancel",
            new { projectId, runId },
            cancellationToken);
        if (idempotency.Replay is not null)
        {
            return idempotency.Replay;
        }

        if (run.Status is ReportRunStatus.Succeeded or ReportRunStatus.Failed or ReportRunStatus.Cancelled ||
            run.PipelineStage == ReportPipelineStage.Rendering || run.OutputCount > 0)
        {
            return Problem(
                StatusCodes.Status409Conflict,
                "reporting.run.already_final",
                "Report run can no longer be cancelled.");
        }

        var now = clock.UtcNow;
        run.Cancel(now);
        var response = await LoadRunResponseAsync(run, dbContext, cancellationToken);
        var responseJson = JsonSerializer.Serialize(response, CanonicalJson.SerializerOptions);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await sideEffectWriter.WriteAsync(
            dbContext.Database.GetDbConnection(),
            transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(
                    actor.TenantId,
                    projectId,
                    actor.UserId,
                    "CertifiedReportRunCancelled",
                    "ReportRun",
                    run.Id.ToString(),
                    now,
                    new Dictionary<string, object?>
                    {
                        ["attempt"] = run.AttemptCount,
                        ["snapshotId"] = run.SnapshotId
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    "Reporting.ReportRunCancelled",
                    1,
                    now,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotency.Key,
                    "reporting.run.cancel",
                    idempotency.RequestHash,
                    StatusCodes.Status200OK,
                    responseJson,
                    now,
                    now.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> DownloadOutputAsync(
        Guid projectId,
        Guid outputId,
        HttpContext httpContext,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ReportingDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        IAuditTrail auditTrail,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var gate = await GateOutputAccessAsync(
            projectId,
            runtime,
            actor,
            permissionService,
            cancellationToken);
        if (gate is not null)
        {
            return gate;
        }

        var context = await LoadOutputContextAsync(
            actor.TenantId,
            projectId,
            outputId,
            dbContext,
            cancellationToken);
        if (context is null)
        {
            return Results.NotFound(new { code = "reporting.output.not_found" });
        }

        var content = await ReadVerifiedOutputAsync(context, documentDirectory, cancellationToken);
        if (content is null)
        {
            await AuditOutputIntegrityFailureAsync(
                context,
                "Download",
                actor,
                auditTrail,
                clock,
                httpContext.TraceIdentifier,
                cancellationToken);
            return OutputIntegrityProblem();
        }

        await auditTrail.WriteAsync(
            new AuditEntry(
                actor.TenantId,
                projectId,
                actor.UserId,
                "CertifiedReportOutputDownloaded",
                "ReportOutput",
                context.Output.Id.ToString(),
                clock.UtcNow,
                new Dictionary<string, object?>
                {
                    ["runId"] = context.Run.Id,
                    ["format"] = context.Output.Format.ToString(),
                    ["contentType"] = context.Output.ContentType,
                    ["sizeBytes"] = context.Output.SizeBytes,
                    ["sha256"] = context.Output.Sha256,
                    ["manifestSha256"] = context.Output.ManifestSha256,
                    ["classification"] = context.Output.Classification.ToString(),
                    ["archived"] = context.Output.ArchiveState == ReportOutputArchiveState.Archived
                },
                httpContext.TraceIdentifier),
            cancellationToken);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        httpContext.Response.Headers.ETag = $"\"sha256-{context.Output.Sha256}\"";
        return Results.File(
            content.Bytes,
            context.Output.ContentType,
            context.Output.FileName,
            enableRangeProcessing: false);
    }

    private static async Task<IResult> VerifyOutputAsync(
        Guid projectId,
        Guid outputId,
        HttpContext httpContext,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        ReportingDbContext dbContext,
        ISharedDocumentDirectory documentDirectory,
        IAuditTrail auditTrail,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var gate = await GateOutputAccessAsync(
            projectId,
            runtime,
            actor,
            permissionService,
            cancellationToken);
        if (gate is not null)
        {
            return gate;
        }

        var context = await LoadOutputContextAsync(
            actor.TenantId,
            projectId,
            outputId,
            dbContext,
            cancellationToken);
        if (context is null)
        {
            return Results.NotFound(new { code = "reporting.output.not_found" });
        }

        var content = await ReadVerifiedOutputAsync(context, documentDirectory, cancellationToken);
        if (content is null)
        {
            await AuditOutputIntegrityFailureAsync(
                context,
                "Verify",
                actor,
                auditTrail,
                clock,
                httpContext.TraceIdentifier,
                cancellationToken);
            return OutputIntegrityProblem();
        }

        var now = clock.UtcNow;
        await auditTrail.WriteAsync(
            new AuditEntry(
                actor.TenantId,
                projectId,
                actor.UserId,
                "CertifiedReportOutputVerified",
                "ReportOutput",
                context.Output.Id.ToString(),
                now,
                new Dictionary<string, object?>
                {
                    ["runId"] = context.Run.Id,
                    ["sha256"] = context.Output.Sha256,
                    ["manifestSha256"] = context.Output.ManifestSha256,
                    ["result"] = "Valid",
                    ["archived"] = context.Output.ArchiveState == ReportOutputArchiveState.Archived
                },
                httpContext.TraceIdentifier),
            cancellationToken);
        return Results.Ok(new ReportOutputVerificationResponse(
            context.Output.VerificationCode,
            "Valid",
            context.Run.DefinitionCode,
            context.Run.TemplateVersion,
            context.Run.AsOfUtc,
            context.Output.Sha256,
            context.Output.ArchiveState == ReportOutputArchiveState.Archived));
    }

    private static async Task<CatalogGate> GateCatalogAsync(
        Guid projectId,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        CancellationToken cancellationToken)
    {
        if (!runtime.Phase1Enabled)
        {
            return new CatalogGate(Results.NotFound(), false);
        }

        if (!actor.IsAuthenticated)
        {
            return new CatalogGate(Results.Unauthorized(), false);
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "reporting.catalog.read",
                cancellationToken))
        {
            return new CatalogGate(
                Problem(StatusCodes.Status403Forbidden, "reporting.permission.denied", "Report catalog is not permitted."),
                false);
        }

        if (!await projectDirectory.ExistsAsync(actor.TenantId, projectId, cancellationToken))
        {
            return new CatalogGate(Results.NotFound(new { code = "project.not_found" }), false);
        }

        var sourceAllowed = await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            SourcePermission,
            cancellationToken);
        return new CatalogGate(null, sourceAllowed);
    }

    private static async Task<IResult?> GateRunMutationAsync(
        Guid projectId,
        bool requireActiveProject,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        CancellationToken cancellationToken)
    {
        if (!runtime.Phase1Enabled)
        {
            return Results.NotFound();
        }
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                CreateOperation,
                cancellationToken))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "reporting.permission.denied",
                "Report run mutation is not permitted.");
        }
        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                SourcePermission,
                cancellationToken))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "reporting.source_permission.denied",
                "The report source is not permitted.");
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }
        if (requireActiveProject && project.Status != ProjectStatus.Active)
        {
            return Problem(
                StatusCodes.Status409Conflict,
                "project.not_operational",
                "Project is not active.");
        }
        return null;
    }

    private static async Task<IResult?> GateOutputAccessAsync(
        Guid projectId,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        if (!runtime.OutputAccessEnabled)
        {
            return Results.NotFound();
        }
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }
        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "reporting.output.download",
                cancellationToken))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "reporting.permission.denied",
                "Report output access is not permitted.");
        }
        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                SourcePermission,
                cancellationToken))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "reporting.source_permission.denied",
                "The report source is not permitted.");
        }
        return null;
    }

    private static async Task<MutationIdempotency> FindMutationReplayAsync(
        HttpContext httpContext,
        Guid tenantId,
        IIdempotencyStore idempotencyStore,
        string operation,
        object request,
        CancellationToken cancellationToken)
    {
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        IdempotencyKeyRules.Validate(key);
        var requestHash = RequestHash.Create(CanonicalJson.Serialize(request));
        var replay = await idempotencyStore.FindAsync(
            tenantId,
            key,
            operation,
            requestHash,
            cancellationToken);
        return new MutationIdempotency(
            key,
            requestHash,
            replay is null
                ? null
                : Results.Content(
                    replay.ResponseBody,
                    "application/json",
                    Encoding.UTF8,
                    replay.StatusCode));
    }

    private static bool IsExplicitlyRetryable(string? diagnosticCode) => diagnosticCode is
        "reporting.snapshot.transient" or
        "reporting.renderer.transient" or
        "reporting.renderer.license_unconfigured" or
        "reporting.renderer.font_missing" or
        "reporting.run.timeout" or
        "documents.generated.storage_unavailable" or
        "documents.generated.persistence_failed";

    private static async Task<OutputContext?> LoadOutputContextAsync(
        Guid tenantId,
        Guid projectId,
        Guid outputId,
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pair = await (
            from output in dbContext.Outputs.AsNoTracking()
            join run in dbContext.Runs.AsNoTracking() on output.RunId equals run.Id
            where output.Id == outputId &&
                output.TenantId == tenantId &&
                output.ProjectId == projectId &&
                run.TenantId == tenantId &&
                run.ProjectId == projectId &&
                run.Status == ReportRunStatus.Succeeded
            select new { Output = output, Run = run })
            .SingleOrDefaultAsync(cancellationToken);
        if (pair is null)
        {
            return null;
        }

        var snapshot = await dbContext.Snapshots.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == pair.Output.SnapshotId &&
            item.RunId == pair.Run.Id &&
            item.TenantId == tenantId &&
            item.ProjectId == projectId,
            cancellationToken);
        var template = await dbContext.TemplateVersions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == pair.Output.TemplateVersionId &&
            item.DefinitionId == pair.Run.DefinitionId &&
            item.Version == pair.Run.TemplateVersion,
            cancellationToken);
        return new OutputContext(pair.Output, pair.Run, snapshot, template);
    }

    private static async Task<ReleasedDocumentContent?> ReadVerifiedOutputAsync(
        OutputContext context,
        ISharedDocumentDirectory documentDirectory,
        CancellationToken cancellationToken)
    {
        var snapshot = context.Snapshot;
        var template = context.Template;
        if (snapshot is null || template is null)
        {
            return null;
        }

        ReleasedDocumentContent? content;
        try
        {
            content = await documentDirectory.ReadReleasedAsync(
                context.Output.TenantId,
                context.Output.GeneratedDocumentId,
                DocumentOwnerType.ReportOutput,
                context.Output.Id,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
        if (content is null)
        {
            return null;
        }

        var manifest = ReportArtifactIdentity.CreateManifest(
            context.Run,
            snapshot,
            template,
            context.Output.Format);
        var expectedClassification = context.Output.Classification switch
        {
            ReportClassification.Internal => DocumentClassification.Internal,
            ReportClassification.Confidential => DocumentClassification.Confidential,
            ReportClassification.Restricted => DocumentClassification.Restricted,
            _ => (DocumentClassification?)null
        };
        var valid = content.Document.Id == context.Output.GeneratedDocumentId &&
            content.Document.TenantId == context.Output.TenantId &&
            content.Document.ProjectId == context.Output.ProjectId &&
            content.Document.OwnerType == DocumentOwnerType.ReportOutput &&
            content.Document.OwnerId == context.Output.Id &&
            content.Document.VersionNumber == 1 &&
            string.Equals(content.Document.OriginalFileName, context.Output.FileName, StringComparison.Ordinal) &&
            string.Equals(content.Document.ContentType, context.Output.ContentType, StringComparison.OrdinalIgnoreCase) &&
            content.Document.SizeBytes == context.Output.SizeBytes &&
            string.Equals(content.Document.Sha256, context.Output.Sha256, StringComparison.Ordinal) &&
            content.Document.Classification == expectedClassification &&
            content.Document.RetentionPolicy.ToString() == context.Output.RetentionPolicy &&
            string.Equals(manifest.Sha256, context.Output.ManifestSha256, StringComparison.Ordinal) &&
            string.Equals(manifest.VerificationCode, context.Output.VerificationCode, StringComparison.Ordinal) &&
            content.Bytes.LongLength == context.Output.SizeBytes &&
            string.Equals(ReportArtifactIdentity.Sha256(content.Bytes), context.Output.Sha256, StringComparison.Ordinal);
        return valid ? content : null;
    }

    private static IResult OutputIntegrityProblem() => Problem(
        StatusCodes.Status502BadGateway,
        "reporting.output.integrity_failed",
        "Report output integrity verification failed.");

    private static Task AuditOutputIntegrityFailureAsync(
        OutputContext context,
        string operation,
        ICurrentActor actor,
        IAuditTrail auditTrail,
        IClock clock,
        string correlationId,
        CancellationToken cancellationToken) => auditTrail.WriteAsync(
        new AuditEntry(
            actor.TenantId,
            context.Output.ProjectId,
            actor.UserId,
            "CertifiedReportOutputIntegrityFailed",
            "ReportOutput",
            context.Output.Id.ToString(),
            clock.UtcNow,
            new Dictionary<string, object?>
            {
                ["runId"] = context.Run.Id,
                ["operation"] = operation,
                ["format"] = context.Output.Format.ToString(),
                ["expectedSha256"] = context.Output.Sha256,
                ["manifestSha256"] = context.Output.ManifestSha256,
                ["result"] = "Invalid"
            },
            correlationId),
        cancellationToken);

    private static async Task<IResult?> GateRunReadAsync(
        Guid projectId,
        ReportingRuntimeOptions runtime,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        CancellationToken cancellationToken)
    {
        if (!runtime.Phase1Enabled)
        {
            return Results.NotFound();
        }

        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "reporting.catalog.read",
                cancellationToken))
        {
            return Problem(StatusCodes.Status403Forbidden, "reporting.permission.denied", "Report runs are not permitted.");
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                SourcePermission,
                cancellationToken))
        {
            return Problem(
                StatusCodes.Status403Forbidden,
                "reporting.source_permission.denied",
                "The report source is not permitted.");
        }

        return null;
    }

    private static async Task<ReportCatalogDefinitionResponse> ToCatalogResponseAsync(
        ReportDefinitionRecord definition,
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.TemplateVersions.AsNoTracking().SingleAsync(
            item => item.Id == definition.CurrentTemplateVersionId && !item.RetiredAt.HasValue,
            cancellationToken);
        return new ReportCatalogDefinitionResponse(
            definition.Code,
            definition.Title,
            definition.Description,
            definition.Scope,
            definition.Classification,
            definition.ParameterSchemaVersion,
            template.Version,
            Deserialize<ReportFormat[]>(definition.SupportedFormatsJson),
            Deserialize<string[]>(definition.RequiredPermissionsJson),
            [ReportDataStatus.Available, ReportDataStatus.NoData, ReportDataStatus.InsufficientData]);
    }

    private static async Task<ReportRunResponse> LoadRunResponseAsync(
        ReportRun run,
        ReportingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var snapshot = run.SnapshotId.HasValue
            ? await dbContext.Snapshots.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == run.SnapshotId.Value &&
                    item.TenantId == run.TenantId &&
                    item.ProjectId == run.ProjectId,
                cancellationToken)
            : null;
        var outputs = await dbContext.Outputs.AsNoTracking()
            .Where(item => item.RunId == run.Id && item.TenantId == run.TenantId && item.ProjectId == run.ProjectId)
            .OrderBy(item => item.Format)
            .Select(item => new ReportOutputMetadataResponse(
                item.Id,
                item.Format,
                item.FileName,
                item.ContentType,
                item.SizeBytes,
                item.Sha256,
                item.VerificationCode,
                $"/api/v1/projects/{run.ProjectId}/reports/outputs/{item.Id}/content"))
            .ToArrayAsync(cancellationToken);
        return ToRunResponse(run, snapshot, outputs);
    }

    private static ReportRunResponse ToRunResponse(
        ReportRun run,
        ReportSnapshot? snapshot,
        IReadOnlyCollection<ReportOutputMetadataResponse> outputs) => new(
        run.Id,
        run.ProjectId,
        run.DefinitionCode,
        run.TemplateVersion,
        run.Status,
        run.PipelineStage,
        snapshot?.DataStatus,
        run.AsOfUtc,
        Deserialize<ReportFormat[]>(run.RequestedFormatsJson),
        run.AttemptCount,
        run.CreatedAt,
        run.StartedAt,
        run.CompletedAt,
        run.DiagnosticCode,
        snapshot?.Sha256,
        outputs,
        new ReportRunLinks($"/api/v1/projects/{run.ProjectId}/reports/runs/{run.Id}"));

    internal static string SerializePermissionSnapshot(EffectivePermissionPreview preview) =>
        CanonicalJson.Serialize(new ReportPermissionSnapshot(
            preview.PolicyVersion,
            preview.UserId,
            preview.ProjectId,
            preview.EvaluatedAt.ToUniversalTime(),
            preview.Decisions
                .OrderBy(decision => decision.Operation, StringComparer.Ordinal)
                .Select(decision => new ReportPermissionDecision(
                    decision.Operation,
                    decision.Allowed,
                    decision.Source,
                    decision.Scope,
                    decision.Condition,
                    decision.DenyReason,
                    decision.ExpiresAt?.ToUniversalTime()))
                .ToArray()));

    private static ReportFormat[]? NormalizeFormats(ReportFormat[]? formats)
    {
        if (formats is null || formats.Length == 0)
        {
            return null;
        }

        var normalized = formats.Distinct().OrderBy(format => format).ToArray();
        return normalized.Length is >= 1 and <= 2 &&
            normalized.All(format => format is ReportFormat.Pdf or ReportFormat.Xlsx)
                ? normalized
                : null;
    }

    private static DailyReportReportParameters? ParseDailyParameters(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object ||
            parameters.EnumerateObject().Any(property => !DailyParameterNames.Contains(property.Name)) ||
            !parameters.TryGetProperty("dailyReportId", out var reportIdElement) ||
            reportIdElement.ValueKind != JsonValueKind.String ||
            !reportIdElement.TryGetGuid(out var reportId) ||
            reportId == Guid.Empty)
        {
            return null;
        }

        var includeRevisionChain = true;
        if (parameters.TryGetProperty("includeRevisionChain", out var includeElement))
        {
            if (includeElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return null;
            }

            includeRevisionChain = includeElement.GetBoolean();
        }

        return new DailyReportReportParameters(reportId, includeRevisionChain);
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, CanonicalJson.SerializerOptions)
        ?? throw new InvalidOperationException("Stored reporting JSON is invalid.");

    private static IResult Problem(int statusCode, string code, string title) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    private sealed record CatalogGate(IResult? Result, bool SourceAllowed);

    private sealed record MutationIdempotency(
        string Key,
        string RequestHash,
        IResult? Replay);

    private sealed record OutputContext(
        ReportOutput Output,
        ReportRun Run,
        ReportSnapshot? Snapshot,
        ReportTemplateVersionRecord? Template);

    private sealed record CreateRunIdentity(
        Guid ProjectId,
        Guid ClientGeneratedId,
        string DefinitionCode,
        string TemplateVersion,
        DateTimeOffset AsOfUtc,
        IReadOnlyCollection<ReportFormat> Formats,
        DailyReportReportParameters Parameters);
}
