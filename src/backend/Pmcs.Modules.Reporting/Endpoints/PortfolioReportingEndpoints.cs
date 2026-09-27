using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Persistence;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Modules.Reporting.Endpoints;

internal static class PortfolioReportingEndpoints
{
    private const string CreateOperation = "reporting.run.create";
    private const string ReadOperation = "reporting.catalog.read";
    private const string BasePath = "/api/v1/portfolio/reports";

    public static void MapPortfolioReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/portfolio/reports")
            .WithTags("Reporting Center Portfolio");
        group.MapGet("/catalog", ListCatalogAsync);
        group.MapGet("/catalog/{definitionCode}", GetDefinitionAsync);
        group.MapPost("/runs", CreateRunAsync);
        group.MapGet("/runs", ListRunsAsync);
        group.MapGet("/runs/{runId:guid}", GetRunAsync);
    }

    private static async Task<IResult> ListCatalogAsync(
        ReportingRuntimeOptions runtime, ICurrentActor actor,
        IProjectPermissionService permissions, ReportingDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(runtime, actor, permissions, ReadOperation, cancellationToken);
        if (gate is not null) return gate;
        var definition = await ActiveDefinitionAsync(db, cancellationToken);
        return Results.Ok(definition is null
            ? Array.Empty<ReportCatalogDefinitionResponse>()
            : new[] { await ToCatalogAsync(definition, db, cancellationToken) });
    }

    private static async Task<IResult> GetDefinitionAsync(
        string definitionCode, ReportingRuntimeOptions runtime, ICurrentActor actor,
        IProjectPermissionService permissions, ReportingDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(runtime, actor, permissions, ReadOperation, cancellationToken);
        if (gate is not null) return gate;
        if (definitionCode != PortfolioSummaryReportRuntimeContract.DefinitionCode)
            return Results.NotFound(new { code = "reporting.definition.not_found" });
        var definition = await ActiveDefinitionAsync(db, cancellationToken);
        return definition is null ? Results.NotFound(new { code = "reporting.definition.not_found" })
            : Results.Ok(await ToCatalogAsync(definition, db, cancellationToken));
    }

    private static async Task<IResult> CreateRunAsync(
        CreateReportRunRequest request, HttpContext httpContext,
        ReportingRuntimeOptions runtime, ICurrentActor actor,
        IProjectPermissionService permissions, PortfolioSummaryReportSource source,
        ReportingDbContext db, IClock clock,
        ITransactionalSideEffectWriter sideEffects, IIdempotencyStore idempotency,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(runtime, actor, permissions, CreateOperation, cancellationToken);
        if (gate is not null) return gate;
        if (request.ClientGeneratedId == Guid.Empty ||
            request.DefinitionCode != PortfolioSummaryReportRuntimeContract.DefinitionCode ||
            request.TemplateVersion != PortfolioSummaryReportRuntimeContract.TemplateVersion)
            return Problem(StatusCodes.Status400BadRequest, "reporting.definition.invalid",
                "Portfolio definition, template or run identity is invalid.");
        if (request.Parameters.ValueKind != JsonValueKind.Object ||
            request.Parameters.EnumerateObject().Any())
            return Problem(StatusCodes.Status400BadRequest, "reporting.parameters.invalid",
                "Portfolio parameters must be an empty object.");
        var formats = NormalizeFormats(request.Formats);
        if (formats is null)
            return Problem(StatusCodes.Status400BadRequest, "reporting.format.unsupported",
                "Requested format is unsupported.");
        var now = clock.UtcNow.ToUniversalTime();
        var requestedCutoff = request.AsOfUtc?.ToUniversalTime() ?? now;
        if (requestedCutoff > now)
            return Problem(StatusCodes.Status400BadRequest, "reporting.as_of.future",
                "Report cutoff cannot be in the future.");
        var asOfUtc = PortfolioSummaryReportRuntimeContract.NormalizeCutoff(requestedCutoff);

        var definition = await ActiveDefinitionAsync(db, cancellationToken);
        if (definition is null)
            return Problem(StatusCodes.Status400BadRequest, "reporting.definition.invalid",
                "Portfolio definition is unavailable.");
        var template = await db.TemplateVersions.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == definition.CurrentTemplateVersionId && !item.RetiredAt.HasValue,
            cancellationToken);
        if (template is null || template.Version != request.TemplateVersion ||
            template.ContentDigest != PortfolioSummaryReportRuntimeContract.TemplateContentDigest ||
            template.RendererContractVersion != PortfolioSummaryReportRuntimeContract.RendererContractVersion ||
            template.LayoutContractVersion != PortfolioSummaryReportRuntimeContract.LayoutContractVersion)
            return Problem(StatusCodes.Status409Conflict, "reporting.template.invalid",
                "Portfolio template is unavailable.");

        var existing = await db.Runs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == request.ClientGeneratedId && item.TenantId == actor.TenantId,
            cancellationToken);
        if (existing is not null && (existing.Scope != ReportDefinitionScope.Portfolio ||
            existing.RequestedBy != actor.UserId ||
            existing.DefinitionCode != PortfolioSummaryReportRuntimeContract.DefinitionCode))
            return Problem(StatusCodes.Status409Conflict, "reporting.run.identity.conflict",
                "Run identity is already in use.");

        PortfolioPinnedCohort pinned;
        if (existing is not null)
        {
            var previous = ParsePinned(existing);
            if (previous is null || !await source.CanAccessPinnedAsync(
                    previous, actor.UserId, cancellationToken))
                return Problem(StatusCodes.Status403Forbidden, "reporting.source_permission.denied",
                    "Pinned Portfolio source is not permitted.");
            pinned = previous;
        }
        else
        {
            try
            {
                pinned = await source.PinAsync(actor.TenantId, actor.UserId,
                    asOfUtc, cancellationToken);
            }
            catch (DomainRuleException exception)
            {
                return Problem(StatusCodes.Status409Conflict, exception.Code, exception.Message);
            }
        }

        var parametersJson = CanonicalJson.Serialize(new { });
        var pinnedJson = CanonicalJson.Serialize(pinned);
        var formatsJson = CanonicalJson.Serialize(formats);
        var requestHash = RequestHash.Create(CanonicalJson.Serialize(new
        {
            actor.TenantId, actor.UserId, request.ClientGeneratedId,
            request.DefinitionCode, request.TemplateVersion,
            requestedAsOfUtc = request.AsOfUtc?.ToUniversalTime(), formats,
            parametersJson, pinnedCohortSha256 = CanonicalJson.Sha256(pinnedJson)
        }));
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        IdempotencyKeyRules.Validate(key);
        var replay = await idempotency.FindAsync(actor.TenantId, key,
            CreateOperation, requestHash, cancellationToken);
        if (replay is not null)
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8,
                replay.StatusCode);
        if (existing is not null)
            return Problem(StatusCodes.Status409Conflict, "reporting.run.identity.conflict",
                "Run identity is already in use.");

        var permissionSnapshotJson = CanonicalJson.Serialize(new
        {
            actorUserId = actor.UserId, tenantId = actor.TenantId,
            cohortSha256 = CanonicalJson.Sha256(pinnedJson),
            evaluatedAt = now, operation = CreateOperation
        });
        var run = ReportRun.QueuePortfolio(request.ClientGeneratedId, actor.TenantId,
            definition.Id, definition.Code, template.Id, template.Version, parametersJson,
            CanonicalJson.Sha256(parametersJson), formatsJson, asOfUtc, pinnedJson,
            actor.UserId, permissionSnapshotJson, httpContext.TraceIdentifier,
            CanonicalJson.Sha256(key), now);
        db.Runs.Add(run);
        var response = ToResponse(run, null, []);
        var responseJson = JsonSerializer.Serialize(response, CanonicalJson.SerializerOptions);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await sideEffects.WriteAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
            new TransactionalSideEffectBatch(
                new AuditEntry(actor.TenantId, null, actor.UserId,
                    "CertifiedPortfolioReportRunQueued", "ReportRun", run.Id.ToString(), now,
                    new Dictionary<string, object?>
                    {
                        ["definitionCode"] = run.DefinitionCode,
                        ["templateVersion"] = run.TemplateVersion,
                        ["asOfUtc"] = run.AsOfUtc,
                        ["cohortSha256"] = CanonicalJson.Sha256(pinnedJson),
                        ["formats"] = formats.Select(format => format.ToString()).ToArray()
                    }, httpContext.TraceIdentifier),
                new OutboxEnvelope(Guid.NewGuid(), actor.TenantId, null,
                    "Reporting.ReportRunQueued", 1, now, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(actor.TenantId, key, CreateOperation, requestHash,
                    StatusCodes.Status202Accepted, responseJson, now, now.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Accepted(response.Links.Self, response);
    }

    private static async Task<IResult> ListRunsAsync(
        ReportRunStatus? status, int? limit, ReportingRuntimeOptions runtime,
        ICurrentActor actor, IProjectPermissionService permissions,
        PortfolioSummaryReportSource source, ReportingDbContext db,
        CancellationToken cancellationToken)
    {
        var gate = await GateAsync(runtime, actor, permissions, ReadOperation, cancellationToken);
        if (gate is not null) return gate;
        if (status.HasValue && !Enum.IsDefined(status.Value))
            return Problem(StatusCodes.Status400BadRequest, "reporting.status.invalid", "Run status is invalid.");
        var query = db.Runs.AsNoTracking().Where(item => item.TenantId == actor.TenantId &&
            item.Scope == ReportDefinitionScope.Portfolio &&
            item.DefinitionCode == PortfolioSummaryReportRuntimeContract.DefinitionCode);
        if (status.HasValue) query = query.Where(item => item.Status == status.Value);
        var runs = await query.OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id).Take(Math.Clamp(limit ?? 50, 1, 100))
            .ToArrayAsync(cancellationToken);
        var responses = new List<PortfolioReportRunResponse>();
        foreach (var run in runs)
        {
            var pinned = ParsePinned(run);
            if (pinned is not null && await source.CanAccessPinnedAsync(
                    pinned, actor.UserId, cancellationToken))
                responses.Add(await LoadResponseAsync(run, db, cancellationToken));
        }
        return Results.Ok(responses);
    }

    private static async Task<IResult> GetRunAsync(
        Guid runId, ReportingRuntimeOptions runtime, ICurrentActor actor,
        IProjectPermissionService permissions, PortfolioSummaryReportSource source,
        ReportingDbContext db, CancellationToken cancellationToken)
    {
        var gate = await GateAsync(runtime, actor, permissions, ReadOperation, cancellationToken);
        if (gate is not null) return gate;
        var run = await db.Runs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == runId && item.TenantId == actor.TenantId &&
            item.Scope == ReportDefinitionScope.Portfolio &&
            item.DefinitionCode == PortfolioSummaryReportRuntimeContract.DefinitionCode,
            cancellationToken);
        if (run is null) return Results.NotFound(new { code = "reporting.run.not_found" });
        var pinned = ParsePinned(run);
        if (pinned is null || !await source.CanAccessPinnedAsync(pinned,
                actor.UserId, cancellationToken))
            return Problem(StatusCodes.Status403Forbidden, "reporting.source_permission.denied",
                "Pinned Portfolio source is not permitted.");
        return Results.Ok(await LoadResponseAsync(run, db, cancellationToken));
    }

    private static async Task<IResult?> GateAsync(ReportingRuntimeOptions runtime,
        ICurrentActor actor, IProjectPermissionService permissions, string operation,
        CancellationToken cancellationToken)
    {
        if (!runtime.Phase1Enabled) return Results.NotFound();
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await permissions.HasTenantPermissionAsync(actor.TenantId, actor.UserId,
                operation, cancellationToken) ||
            !await permissions.HasTenantPermissionAsync(actor.TenantId, actor.UserId,
                "portfolio.read", cancellationToken))
            return Problem(StatusCodes.Status403Forbidden, "reporting.permission.denied",
                "Portfolio report access is not permitted.");
        return null;
    }

    private static Task<ReportDefinitionRecord?> ActiveDefinitionAsync(
        ReportingDbContext db, CancellationToken cancellationToken) =>
        db.Definitions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Code == PortfolioSummaryReportRuntimeContract.DefinitionCode &&
            item.Scope == ReportDefinitionScope.Portfolio &&
            item.Status == ReportDefinitionStatus.Active, cancellationToken);

    private static async Task<ReportCatalogDefinitionResponse> ToCatalogAsync(
        ReportDefinitionRecord definition, ReportingDbContext db,
        CancellationToken cancellationToken)
    {
        var template = await db.TemplateVersions.AsNoTracking().SingleAsync(item =>
            item.Id == definition.CurrentTemplateVersionId && !item.RetiredAt.HasValue,
            cancellationToken);
        return new ReportCatalogDefinitionResponse(definition.Code, definition.Title,
            definition.Description, definition.Scope, definition.Classification,
            definition.ParameterSchemaVersion, template.Version,
            JsonSerializer.Deserialize<ReportFormat[]>(definition.SupportedFormatsJson,
                CanonicalJson.SerializerOptions) ?? [],
            JsonSerializer.Deserialize<string[]>(definition.RequiredPermissionsJson,
                CanonicalJson.SerializerOptions) ?? [],
            [ReportDataStatus.Available, ReportDataStatus.NoData,
             ReportDataStatus.InsufficientData]);
    }

    private static ReportFormat[]? NormalizeFormats(ReportFormat[]? formats) =>
        formats is { Length: > 0 and <= 2 } &&
        formats.All(item => item is ReportFormat.Pdf or ReportFormat.Xlsx) &&
        formats.Distinct().Count() == formats.Length
            ? formats.Order().ToArray() : null;

    private static PortfolioPinnedCohort? ParsePinned(ReportRun run)
    {
        try
        {
            var pinned = JsonSerializer.Deserialize<PortfolioPinnedCohort>(
                run.PinnedPortfolioCohortJson!, CanonicalJson.SerializerOptions);
            pinned?.Validate();
            return pinned is not null && pinned.TenantId == run.TenantId &&
                pinned.RequestedBy == run.RequestedBy && pinned.AsOfUtc == run.AsOfUtc
                ? pinned : null;
        }
        catch (Exception exception) when (exception is JsonException or DomainRuleException or
            ArgumentException or NullReferenceException) { return null; }
    }

    private static async Task<PortfolioReportRunResponse> LoadResponseAsync(
        ReportRun run, ReportingDbContext db, CancellationToken cancellationToken)
    {
        var snapshot = run.SnapshotId.HasValue
            ? await db.Snapshots.AsNoTracking().SingleOrDefaultAsync(item =>
                item.Id == run.SnapshotId.Value && item.RunId == run.Id &&
                item.TenantId == run.TenantId && item.Scope == ReportDefinitionScope.Portfolio &&
                item.ProjectId == null, cancellationToken)
            : null;
        var outputs = await db.Outputs.AsNoTracking().Where(item =>
            item.RunId == run.Id && item.TenantId == run.TenantId &&
            item.Scope == ReportDefinitionScope.Portfolio && item.ProjectId == null)
            .OrderBy(item => item.Format)
            .Select(item => new ReportOutputMetadataResponse(item.Id, item.Format,
                item.FileName, item.ContentType, item.SizeBytes, item.Sha256,
                item.VerificationCode, $"{BasePath}/outputs/{item.Id}/content"))
            .ToArrayAsync(cancellationToken);
        return ToResponse(run, snapshot, outputs);
    }

    private static PortfolioReportRunResponse ToResponse(ReportRun run,
        ReportSnapshot? snapshot, IReadOnlyCollection<ReportOutputMetadataResponse> outputs) => new(
        run.Id, run.DefinitionCode, run.TemplateVersion, run.Status, run.PipelineStage,
        snapshot?.DataStatus, run.AsOfUtc,
        JsonSerializer.Deserialize<ReportFormat[]>(run.RequestedFormatsJson,
            CanonicalJson.SerializerOptions) ?? [],
        run.AttemptCount, run.CreatedAt, run.StartedAt, run.CompletedAt,
        run.DiagnosticCode, snapshot?.Sha256, outputs,
        new ReportRunLinks($"{BasePath}/runs/{run.Id}"));

    private static IResult Problem(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

public sealed record PortfolioReportRunResponse(
    Guid Id, string DefinitionCode, string TemplateVersion,
    ReportRunStatus Status, ReportPipelineStage PipelineStage,
    ReportDataStatus? DataStatus, DateTimeOffset AsOfUtc,
    IReadOnlyCollection<ReportFormat> RequestedFormats, int AttemptCount,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt, string? DiagnosticCode, string? SnapshotHash,
    IReadOnlyCollection<ReportOutputMetadataResponse> Outputs, ReportRunLinks Links);
