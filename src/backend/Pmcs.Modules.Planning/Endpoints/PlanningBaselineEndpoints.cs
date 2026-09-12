using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Planning.Persistence;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Planning.Endpoints;

internal static partial class PlanningEndpoints
{
    private static void MapPlanningBasisEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/baselines", ListBaselinesAsync);
        group.MapPost("/baselines", CreateBaselineAsync);
        group.MapPut("/baselines/{baselineId:guid}", AmendBaselineAsync);
        group.MapPost("/baselines/{baselineId:guid}/submit", SubmitBaselineAsync);
        group.MapPost("/baselines/{baselineId:guid}/approve", ApproveBaselineAsync);
        group.MapPost("/baselines/{baselineId:guid}/return", ReturnBaselineAsync);

        group.MapGet("/milestone-updates", ListMilestoneUpdatesAsync);
        group.MapPost("/milestone-updates", CreateMilestoneUpdateAsync);
        group.MapPut("/milestone-updates/{updateId:guid}", AmendMilestoneUpdateAsync);
        group.MapPost("/milestone-updates/{updateId:guid}/submit", SubmitMilestoneUpdateAsync);
        group.MapPost("/milestone-updates/{updateId:guid}/approve", ApproveMilestoneUpdateAsync);
        group.MapPost("/milestone-updates/{updateId:guid}/return", ReturnMilestoneUpdateAsync);
    }

    private static async Task<IResult> ListBaselinesAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        PlanningDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.baselines.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var items = await dbContext.PlanningBaselines
            .AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(PlanningBaselineResponse.From).ToArray());
    }

    private static async Task<IResult> CreateBaselineAsync(
        Guid projectId,
        CreatePlanningBaselineRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.baselines.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.baselines.create:{projectId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (!Matches(request.Kind, project.PlanningMode))
        {
            return BaselineModeMismatch(project.PlanningMode);
        }

        var versionCode = request.VersionCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (await dbContext.PlanningBaselines.AnyAsync(
                item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                    item.VersionCode == versionCode,
                cancellationToken))
        {
            return Results.Conflict(new { code = "planning.baseline.version.duplicate" });
        }

        var entries = request.Entries?.Select(item => item.ToDomain()).ToArray()
            ?? Array.Empty<PlanningBaselineEntry>();
        var mappingFailure = await ValidateMeasurementMappingsAsync(
            dbContext, actor.TenantId, projectId, entries, false, cancellationToken);
        if (mappingFailure is not null)
        {
            return mappingFailure;
        }

        var baseline = PlanningBaseline.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            versionCode,
            request.Title,
            request.Kind,
            request.SourceSystem,
            request.SourceReference,
            entries,
            actor.UserId,
            clock.UtcNow);
        dbContext.PlanningBaselines.Add(baseline);
        var response = PlanningBaselineResponse.From(baseline);
        await PersistPlanningAsync(
            dbContext,
            httpContext,
            actor,
            projectId,
            "PlanningBaseline",
            baseline.Id,
            "PlanningBaselineCreated",
            BaselineAuditData(baseline),
            response,
            idempotency,
            StatusCodes.Status201Created,
            sideEffectWriter,
            clock,
            cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/planning/baselines/{baseline.Id}", response);
    }

    private static async Task<IResult> AmendBaselineAsync(
        Guid projectId,
        Guid baselineId,
        AmendPlanningBaselineRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.baselines.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.baselines.amend:{baselineId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (!baseline.Matches(project.PlanningMode))
        {
            return BaselineModeMismatch(project.PlanningMode);
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.baseline.revision.conflict", baseline.Revision);
        }

        var entries = request.Entries?.Select(item => item.ToDomain()).ToArray()
            ?? Array.Empty<PlanningBaselineEntry>();
        var mappingFailure = await ValidateMeasurementMappingsAsync(
            dbContext, actor.TenantId, projectId, entries, false, cancellationToken);
        if (mappingFailure is not null)
        {
            return mappingFailure;
        }

        baseline.Amend(
            request.BaseRevision,
            request.Title,
            request.SourceSystem,
            request.SourceReference,
            entries);
        var response = PlanningBaselineResponse.From(baseline);
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "PlanningBaseline", baseline.Id,
            "PlanningBaselineAmended", BaselineAuditData(baseline), response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static Task<IResult> SubmitBaselineAsync(
        Guid projectId,
        Guid baselineId,
        SubmitPlanningItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken) =>
        TransitionBaselineAsync(
            projectId,
            baselineId,
            request,
            httpContext,
            actor,
            permissionService,
            projectDirectory,
            dbContext,
            clock,
            sideEffectWriter,
            idempotencyStore,
            "planning.baselines.submit",
            "submit",
            cancellationToken);

    private static async Task<IResult> ApproveBaselineAsync(
        Guid projectId,
        Guid baselineId,
        ReviewPlanningItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.baselines.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.baselines.approve:{baselineId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (!baseline.Matches(project.PlanningMode))
        {
            return BaselineModeMismatch(project.PlanningMode);
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.baseline.revision.conflict", baseline.Revision);
        }

        var mappingFailure = await ValidateMeasurementMappingsAsync(
            dbContext, actor.TenantId, projectId, baseline.Entries, true, cancellationToken);
        if (mappingFailure is not null)
        {
            return mappingFailure;
        }

        var previous = await dbContext.PlanningBaselines
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                item.Status == PlanningBaselineStatus.Approved && item.Id != baseline.Id)
            .ToListAsync(cancellationToken);
        foreach (var item in previous)
        {
            item.Supersede(actor.UserId, clock.UtcNow);
        }

        baseline.Approve(request.BaseRevision, request.Comment, actor.UserId, clock.UtcNow);
        var response = PlanningBaselineResponse.From(baseline);
        var audit = BaselineAuditData(baseline);
        audit["supersededBaselineIds"] = previous.Select(item => item.Id).ToArray();
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "PlanningBaseline", baseline.Id,
            "PlanningBaselineApproved", audit, response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ReturnBaselineAsync(
        Guid projectId,
        Guid baselineId,
        ReturnPlanningItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.baselines.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.baselines.return:{baselineId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.baseline.revision.conflict", baseline.Revision);
        }

        baseline.ReturnForCorrection(request.BaseRevision, request.Reason, actor.UserId, clock.UtcNow);
        var response = PlanningBaselineResponse.From(baseline);
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "PlanningBaseline", baseline.Id,
            "PlanningBaselineReturned", BaselineAuditData(baseline), response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> TransitionBaselineAsync(
        Guid projectId,
        Guid baselineId,
        SubmitPlanningItemRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        PlanningDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        string permission,
        string transition,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, permission, cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.baselines.{transition}:{baselineId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, baselineId, cancellationToken);
        if (baseline is null)
        {
            return Results.NotFound();
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        if (!baseline.Matches(project.PlanningMode))
        {
            return BaselineModeMismatch(project.PlanningMode);
        }

        if (baseline.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.baseline.revision.conflict", baseline.Revision);
        }

        baseline.Submit(request.BaseRevision, clock.UtcNow);
        var response = PlanningBaselineResponse.From(baseline);
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "PlanningBaseline", baseline.Id,
            "PlanningBaselineSubmitted", BaselineAuditData(baseline), response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult?> ValidateMeasurementMappingsAsync(
        PlanningDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        IReadOnlyCollection<PlanningBaselineEntry> entries,
        bool requireApprovalReadiness,
        CancellationToken cancellationToken)
    {
        var mappedIds = entries
            .Where(item => item.MeasurementMethod == ProgressMeasurementMethod.QuantityBased)
            .Select(item => item.MeasurementItemId!.Value)
            .Distinct()
            .ToArray();
        if (mappedIds.Length == 0)
        {
            return null;
        }

        var mapped = await dbContext.MeasurementItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && mappedIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (mapped.Count != mappedIds.Length)
        {
            return Results.UnprocessableEntity(new { code = "planning.baseline.measurement_mapping.invalid" });
        }

        if (requireApprovalReadiness && mapped.Any(item =>
                item.Status != MeasurementItemStatus.Active || !item.TargetQuantity.HasValue))
        {
            return Results.UnprocessableEntity(new { code = "planning.baseline.measurement_basis.not_ready" });
        }

        return null;
    }

    private static Task<PlanningBaseline?> FindBaselineAsync(
        PlanningDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid baselineId,
        CancellationToken cancellationToken) =>
        dbContext.PlanningBaselines.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == baselineId,
            cancellationToken);

    private static bool Matches(PlanningBaselineKind kind, PlanningMode mode) => (kind, mode) switch
    {
        (PlanningBaselineKind.MeasurementWeights, PlanningMode.SimpleWorkList) => true,
        (PlanningBaselineKind.MilestonePlan, PlanningMode.Milestones) => true,
        (PlanningBaselineKind.WbsBaseline, PlanningMode.WbsBaseline) => true,
        (PlanningBaselineKind.ExternalSchedule, PlanningMode.ExternalSchedule) => true,
        _ => false
    };

    private static IResult BaselineModeMismatch(PlanningMode currentMode) => Results.Conflict(new
    {
        code = "planning.baseline.mode_mismatch",
        currentMode
    });

    private static IResult PlanningRevisionConflict(string code, long currentRevision) => Results.Conflict(new
    {
        code,
        currentRevision
    });

    private static Dictionary<string, object?> BaselineAuditData(PlanningBaseline baseline) => new()
    {
        ["versionCode"] = baseline.VersionCode,
        ["title"] = baseline.Title,
        ["kind"] = baseline.Kind.ToString(),
        ["status"] = baseline.Status.ToString(),
        ["entryCount"] = baseline.Entries.Count,
        ["sourceSystem"] = baseline.SourceSystem,
        ["sourceReference"] = baseline.SourceReference,
        ["revision"] = baseline.Revision
    };

    private static async Task PersistPlanningAsync<TResponse>(
        PlanningDbContext dbContext,
        HttpContext httpContext,
        ICurrentActor actor,
        Guid projectId,
        string entityType,
        Guid entityId,
        string eventType,
        IReadOnlyDictionary<string, object?> auditData,
        TResponse response,
        IdempotencyContext idempotency,
        int statusCode,
        ITransactionalSideEffectWriter sideEffectWriter,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var responseJson = JsonSerializer.Serialize(response, SerializerOptions);
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
                    eventType,
                    entityType,
                    entityId.ToString(),
                    clock.UtcNow,
                    auditData,
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(), actor.TenantId, projectId, $"Planning.{eventType}", 1,
                    clock.UtcNow, responseJson, httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId, idempotency.Key, idempotency.Operation, idempotency.Hash,
                    statusCode, responseJson, clock.UtcNow, clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
