using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.QualitySafety.Domain;
using Pmcs.Modules.QualitySafety.Persistence;
using static Pmcs.Modules.QualitySafety.Endpoints.QualitySafetyEndpointSupport;

namespace Pmcs.Modules.QualitySafety.Endpoints;

internal static partial class QualitySafetyEndpoints
{
    private static async Task<IResult> RequestInspectionAsync(Guid projectId, RequestInspectionRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.inspections.capture", actor, permissions, projects, db, token);
        if (error is not null) return error;
        var command = await CommandAsync(context, actor, idempotency, "quality.inspection.request", request, token);
        if (command.Replay is not null) return command.Replay;
        if (request.InspectionTestPlanVersionId.HasValue && !await db.InspectionTestPlans.AnyAsync(x =>
            x.Id == request.InspectionTestPlanVersionId.Value && x.TenantId == actor.TenantId && x.ProjectId == projectId, token))
            return Results.UnprocessableEntity(new { code = "quality_safety.inspection.plan.invalid" });
        var entity = InspectionRecord.Request(request.ClientGeneratedId, actor.TenantId, projectId, request.SourceIntakeId,
            request.InspectionTestPlanVersionId,
            request.InspectionType, request.Location, request.AcceptanceCriteria, request.ChecklistTemplateReference,
            request.ChecklistTemplateVersion, request.RequestedFor, actor.UserId, clock.UtcNow);
        db.Inspections.Add(entity); var response = InspectionResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "InspectionRecord", "InspectionRequested", response,
            command.Command!, 201, effects, clock, token); return Results.Created($"/api/v1/projects/{projectId}/quality-safety/inspections/{entity.Id}", response);
    }

    private static async Task<IResult> RecordReadinessAsync(Guid projectId, Guid inspectionId,
        RecordInspectionReadinessRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.inspections.capture", actor, permissions, projects, db, token); if (error is not null) return error;
        var entity = await db.Inspections.SingleOrDefaultAsync(x => x.Id == inspectionId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.inspection.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality.inspection.readiness", request, token); if (command.Replay is not null) return command.Replay;
        entity.RecordReadiness(request.BaseRevision, request.Readiness, request.Note); var response = InspectionResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "InspectionRecord", "InspectionReadinessRecorded", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> RecordResultAsync(Guid projectId, Guid inspectionId,
        RecordInspectionResultRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.inspections.result", actor, permissions, projects, db, token); if (error is not null) return error;
        var entity = await db.Inspections.SingleOrDefaultAsync(x => x.Id == inspectionId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.inspection.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality.inspection.result", request, token); if (command.Replay is not null) return command.Replay;
        entity.RecordResult(request.BaseRevision, request.Result, request.Note, request.EvidenceReferences, actor.UserId, clock.UtcNow); var response = InspectionResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "InspectionRecord", "InspectionResultRecorded", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> CreateNcrAsync(Guid projectId, CreateNcrRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.ncr.create", actor, permissions, projects, db, token); if (error is not null) return error;
        var command = await CommandAsync(context, actor, idempotency, "quality.ncr.create", request, token); if (command.Replay is not null) return command.Replay;
        var entity = NonConformanceRecord.Create(request.ClientGeneratedId, actor.TenantId, projectId,
            request.SourceIntakeId, request.InspectionId, request.GoodsReceiptId, request.PurchaseOrderId,
            request.VendorPartyId, request.SupplyItemId, request.LotReference, request.Title,
            request.Requirement, request.NonConformity, actor.UserId, clock.UtcNow);
        db.NonConformances.Add(entity); var response = NcrResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "NonConformanceRecord", "NcrCreated", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/ncrs/{entity.Id}", response);
    }

    private static async Task<IResult> TransitionNcrAsync(Guid projectId, Guid ncrId, TransitionNcrRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var permission = request.TargetStatus == NcrStatus.Closed ? "quality.ncr.verify" : "quality.ncr.manage";
        var error = await AuthorizeArea(projectId, ControlArea.Quality, permission, actor, permissions, projects, db, token); if (error is not null) return error;
        var entity = await db.NonConformances.SingleOrDefaultAsync(x => x.Id == ncrId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.ncr.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality.ncr.transition", request, token); if (command.Replay is not null) return command.Replay;
        entity.Transition(request.BaseRevision, request.TargetStatus, request.Disposition, request.DispositionNote,
            request.ConcessionApprovedBy, request.RootCauseStatus, request.RootCause, request.ClosureEvidence,
            request.ClosureWaiverReason, string.IsNullOrWhiteSpace(request.ClosureWaiverReason) ? null : actor.UserId, clock.UtcNow);
        var response = NcrResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "NonConformanceRecord", "NcrTransitioned", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> CreateDefectAsync(Guid projectId, CreateDefectRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.defects.capture", actor, permissions, projects, db, token); if (error is not null) return error;
        var command = await CommandAsync(context, actor, idempotency, "quality.defect.create", request, token); if (command.Replay is not null) return command.Replay;
        var entity = DefectRecord.Create(request.ClientGeneratedId, actor.TenantId, projectId, request.SourceIntakeId, request.Title, request.Location, actor.UserId, clock.UtcNow);
        db.Defects.Add(entity); var response = DefectResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "DefectRecord", "DefectCreated", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/defects/{entity.Id}", response);
    }

    private static async Task<IResult> AssignDefectAsync(Guid projectId, Guid defectId, AssignDefectRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.defects.manage", actor, permissions, projects, db, token); if (error is not null) return error;
        var entity = await db.Defects.SingleOrDefaultAsync(x => x.Id == defectId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.defect.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality.defect.assign", request, token); if (command.Replay is not null) return command.Replay;
        entity.Assign(request.BaseRevision, request.AssigneeUserId, request.DueDate); var response = DefectResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "DefectRecord", "DefectAssigned", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> TransitionDefectAsync(Guid projectId, Guid defectId, TransitionDefectRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var permission = request.TargetStatus is DefectStatus.Accepted or DefectStatus.Closed ? "quality.defects.verify" : "quality.defects.manage";
        var error = await AuthorizeArea(projectId, ControlArea.Quality, permission, actor, permissions, projects, db, token); if (error is not null) return error;
        var entity = await db.Defects.SingleOrDefaultAsync(x => x.Id == defectId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.defect.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality.defect.transition", request, token); if (command.Replay is not null) return command.Replay;
        entity.Transition(request.BaseRevision, request.TargetStatus, request.EvidenceReferences, clock.UtcNow); var response = DefectResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "DefectRecord", "DefectTransitioned", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult?> AuthorizeArea(Guid projectId, ControlArea area, string permission,
        ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, CancellationToken token)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await Allowed(permissions, actor, projectId, permission, token)) return Results.StatusCode(403);
        var result = await RequireAreaAsync(projectId, area, actor, projects, db, true, token); return result.Error;
    }
}
