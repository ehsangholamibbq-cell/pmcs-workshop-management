using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Planning.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Planning.Endpoints;

internal static partial class PlanningEndpoints
{
    private static async Task<IResult> ListMilestoneUpdatesAsync(
        Guid projectId,
        Guid? baselineId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        PlanningDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.milestones.read", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var query = dbContext.MilestoneProgressUpdates
            .AsNoTracking()
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId);
        if (baselineId.HasValue)
        {
            query = query.Where(item => item.BaselineId == baselineId.Value);
        }

        var items = await query
            .OrderByDescending(item => item.StatusDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(items.Select(MilestoneProgressUpdateResponse.From).ToArray());
    }

    private static async Task<IResult> CreateMilestoneUpdateAsync(
        Guid projectId,
        CreateMilestoneProgressUpdateRequest request,
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

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.milestones.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.milestone_updates.create:{projectId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var baseline = await FindBaselineAsync(
            dbContext, actor.TenantId, projectId, request.BaselineId, cancellationToken);
        var entryFailure = await ValidateActiveManualMilestoneAsync(
            baseline, request.BaselineEntryId, actor.TenantId, projectId, projectDirectory, cancellationToken);
        if (entryFailure is not null)
        {
            return entryFailure;
        }

        var update = MilestoneProgressUpdate.Create(
            request.ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
            actor.TenantId,
            projectId,
            request.BaselineId,
            request.BaselineEntryId,
            request.StatusDate,
            request.ProgressPercent,
            request.EvidenceReference,
            request.Note,
            actor.UserId,
            clock.UtcNow);
        dbContext.MilestoneProgressUpdates.Add(update);
        var response = MilestoneProgressUpdateResponse.From(update);
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "MilestoneProgressUpdate", update.Id,
            "MilestoneProgressUpdateCreated", MilestoneAuditData(update), response, idempotency,
            StatusCodes.Status201Created, sideEffectWriter, clock, cancellationToken);
        return Results.Created($"/api/v1/projects/{projectId}/planning/milestone-updates/{update.Id}", response);
    }

    private static async Task<IResult> AmendMilestoneUpdateAsync(
        Guid projectId,
        Guid updateId,
        AmendMilestoneProgressUpdateRequest request,
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

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.milestones.capture", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.milestone_updates.amend:{updateId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var update = await FindMilestoneUpdateAsync(dbContext, actor.TenantId, projectId, updateId, cancellationToken);
        if (update is null)
        {
            return Results.NotFound();
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, update.BaselineId, cancellationToken);
        var entryFailure = await ValidateActiveManualMilestoneAsync(
            baseline, update.BaselineEntryId, actor.TenantId, projectId, projectDirectory, cancellationToken);
        if (entryFailure is not null)
        {
            return entryFailure;
        }

        if (update.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.milestone_update.revision.conflict", update.Revision);
        }

        update.Amend(
            request.BaseRevision,
            request.StatusDate,
            request.ProgressPercent,
            request.EvidenceReference,
            request.Note);
        var response = MilestoneProgressUpdateResponse.From(update);
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "MilestoneProgressUpdate", update.Id,
            "MilestoneProgressUpdateAmended", MilestoneAuditData(update), response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> SubmitMilestoneUpdateAsync(
        Guid projectId,
        Guid updateId,
        SubmitPlanningItemRequest request,
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

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.milestones.submit", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.milestone_updates.submit:{updateId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var update = await FindMilestoneUpdateAsync(dbContext, actor.TenantId, projectId, updateId, cancellationToken);
        if (update is null)
        {
            return Results.NotFound();
        }

        if (update.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.milestone_update.revision.conflict", update.Revision);
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, update.BaselineId, cancellationToken);
        var entryFailure = await ValidateActiveManualMilestoneAsync(
            baseline, update.BaselineEntryId, actor.TenantId, projectId, projectDirectory, cancellationToken);
        if (entryFailure is not null)
        {
            return entryFailure;
        }

        update.Submit(request.BaseRevision, clock.UtcNow);
        var response = MilestoneProgressUpdateResponse.From(update);
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "MilestoneProgressUpdate", update.Id,
            "MilestoneProgressUpdateSubmitted", MilestoneAuditData(update), response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ApproveMilestoneUpdateAsync(
        Guid projectId,
        Guid updateId,
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

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.milestones.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.milestone_updates.approve:{updateId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var update = await FindMilestoneUpdateAsync(dbContext, actor.TenantId, projectId, updateId, cancellationToken);
        if (update is null)
        {
            return Results.NotFound();
        }

        if (update.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.milestone_update.revision.conflict", update.Revision);
        }

        var baseline = await FindBaselineAsync(dbContext, actor.TenantId, projectId, update.BaselineId, cancellationToken);
        var entryFailure = await ValidateActiveManualMilestoneAsync(
            baseline, update.BaselineEntryId, actor.TenantId, projectId, projectDirectory, cancellationToken);
        if (entryFailure is not null)
        {
            return entryFailure;
        }

        var previous = await dbContext.MilestoneProgressUpdates
            .Where(item => item.TenantId == actor.TenantId && item.ProjectId == projectId &&
                item.BaselineId == update.BaselineId && item.BaselineEntryId == update.BaselineEntryId &&
                item.Status == MilestoneProgressStatus.Approved && item.Id != update.Id)
            .ToListAsync(cancellationToken);
        if (previous.Any(item => item.StatusDate > update.StatusDate))
        {
            return Results.Conflict(new { code = "planning.milestone_update.status_date.regression" });
        }

        foreach (var item in previous)
        {
            item.Supersede(actor.UserId, clock.UtcNow);
        }

        update.Approve(request.BaseRevision, request.Comment, actor.UserId, clock.UtcNow);
        var response = MilestoneProgressUpdateResponse.From(update);
        var audit = MilestoneAuditData(update);
        audit["supersededUpdateIds"] = previous.Select(item => item.Id).ToArray();
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "MilestoneProgressUpdate", update.Id,
            "MilestoneProgressUpdateApproved", audit, response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ReturnMilestoneUpdateAsync(
        Guid projectId,
        Guid updateId,
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

        if (!await HasPermissionAsync(permissionService, actor, projectId, "planning.milestones.review", cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotency = await GetReplayAsync(
            httpContext, actor, idempotencyStore, $"planning.milestone_updates.return:{updateId:N}", request, cancellationToken);
        if (idempotency.Result is not null)
        {
            return idempotency.Result;
        }

        var update = await FindMilestoneUpdateAsync(dbContext, actor.TenantId, projectId, updateId, cancellationToken);
        if (update is null)
        {
            return Results.NotFound();
        }

        if (update.Revision != request.BaseRevision)
        {
            return PlanningRevisionConflict("planning.milestone_update.revision.conflict", update.Revision);
        }

        update.ReturnForCorrection(request.BaseRevision, request.Reason, actor.UserId, clock.UtcNow);
        var response = MilestoneProgressUpdateResponse.From(update);
        await PersistPlanningAsync(
            dbContext, httpContext, actor, projectId, "MilestoneProgressUpdate", update.Id,
            "MilestoneProgressUpdateReturned", MilestoneAuditData(update), response, idempotency,
            StatusCodes.Status200OK, sideEffectWriter, clock, cancellationToken);
        return Results.Ok(response);
    }

    private static IResult? ValidateApprovedManualMilestone(PlanningBaseline? baseline, Guid entryId)
    {
        if (baseline is null)
        {
            return Results.NotFound(new { code = "planning.baseline.not_found" });
        }

        if (baseline.Status != PlanningBaselineStatus.Approved)
        {
            return Results.Conflict(new { code = "planning.milestone_update.baseline_not_approved" });
        }

        var entry = baseline.Entries.SingleOrDefault(item => item.Id == entryId);
        if (entry is null || entry.Kind != PlanningEntryKind.Milestone ||
            entry.MeasurementMethod != ProgressMeasurementMethod.ManualPercent)
        {
            return Results.UnprocessableEntity(new { code = "planning.milestone_update.entry.invalid" });
        }

        return null;
    }

    private static async Task<IResult?> ValidateActiveManualMilestoneAsync(
        PlanningBaseline? baseline,
        Guid entryId,
        Guid tenantId,
        Guid projectId,
        IProjectDirectory projectDirectory,
        CancellationToken cancellationToken)
    {
        var entryFailure = ValidateApprovedManualMilestone(baseline, entryId);
        if (entryFailure is not null)
        {
            return entryFailure;
        }

        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        return baseline!.Matches(project.PlanningMode)
            ? null
            : BaselineModeMismatch(project.PlanningMode);
    }

    private static Task<MilestoneProgressUpdate?> FindMilestoneUpdateAsync(
        PlanningDbContext dbContext,
        Guid tenantId,
        Guid projectId,
        Guid updateId,
        CancellationToken cancellationToken) =>
        dbContext.MilestoneProgressUpdates.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.ProjectId == projectId && item.Id == updateId,
            cancellationToken);

    private static Dictionary<string, object?> MilestoneAuditData(MilestoneProgressUpdate update) => new()
    {
        ["baselineId"] = update.BaselineId,
        ["baselineEntryId"] = update.BaselineEntryId,
        ["statusDate"] = update.StatusDate,
        ["progressPercent"] = update.ProgressPercent,
        ["evidenceReference"] = update.EvidenceReference,
        ["status"] = update.Status.ToString(),
        ["revision"] = update.Revision
    };
}
