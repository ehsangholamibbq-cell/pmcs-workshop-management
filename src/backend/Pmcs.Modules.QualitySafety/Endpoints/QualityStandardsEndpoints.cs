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
    private static async Task<IResult> CreateInspectionTestPlanAsync(Guid projectId,
        CreateInspectionTestPlanRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.standards.manage", actor, permissions, projects, db, token); if (error is not null) return error;
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await db.InspectionTestPlans.AnyAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Code == normalizedCode && x.Version == request.Version, token))
            return Results.Conflict(new { code = "quality_safety.itp.version.exists" });
        var command = await CommandAsync(context, actor, idempotency, "quality.itp.create", request, token); if (command.Replay is not null) return command.Replay;
        var entity = InspectionTestPlanVersion.Create(request.ClientGeneratedId, actor.TenantId, projectId,
            request.Code, request.Version, request.Title, request.Stages, request.AcceptanceCriteria,
            request.InspectorRole, request.PointType, request.EffectiveFrom, actor.UserId, clock.UtcNow);
        db.InspectionTestPlans.Add(entity); var response = InspectionTestPlanResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "InspectionTestPlanVersion", "InspectionTestPlanVersionCreated", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/inspection-test-plans/{entity.Id}", response);
    }

    private static async Task<IResult> CreateChecklistTemplateAsync(Guid projectId,
        CreateChecklistTemplateRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.standards.manage", actor, permissions, projects, db, token); if (error is not null) return error;
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await db.ChecklistTemplates.AnyAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Code == normalizedCode && x.Version == request.Version, token))
            return Results.Conflict(new { code = "quality_safety.checklist.version.exists" });
        var command = await CommandAsync(context, actor, idempotency, "quality.checklist.create", request, token); if (command.Replay is not null) return command.Replay;
        var entity = ChecklistTemplateVersion.Create(request.ClientGeneratedId, actor.TenantId, projectId,
            request.Code, request.Version, request.Title, request.Items, request.EffectiveFrom, actor.UserId, clock.UtcNow);
        db.ChecklistTemplates.Add(entity); var response = ChecklistTemplateResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "ChecklistTemplateVersion", "ChecklistTemplateVersionCreated", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/checklist-templates/{entity.Id}", response);
    }

    private static async Task<IResult> RecordQualityTestAsync(Guid projectId, RecordQualityTestRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Quality, "quality.tests.capture", actor, permissions, projects, db, token); if (error is not null) return error;
        if (request.InspectionId.HasValue && !await db.Inspections.AnyAsync(x => x.Id == request.InspectionId.Value && x.TenantId == actor.TenantId && x.ProjectId == projectId, token))
            return Results.UnprocessableEntity(new { code = "quality_safety.test.inspection.invalid" });
        var command = await CommandAsync(context, actor, idempotency, "quality.test.record", request, token); if (command.Replay is not null) return command.Replay;
        var entity = QualityTestRecord.Record(request.ClientGeneratedId, actor.TenantId, projectId,
            request.InspectionId, request.TestType, request.TestedAt, request.SampleReference,
            request.AcceptanceCriteria, request.Result, request.ResultDetails, request.EvidenceReferences,
            actor.UserId, clock.UtcNow); db.TestRecords.Add(entity); var response = QualityTestResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "QualityTestRecord", "QualityTestRecorded", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/test-records/{entity.Id}", response);
    }

    private static async Task<IResult> RecordCompetencyAsync(Guid projectId, RecordCompetencyRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Hse, "hse.competencies.manage", actor, permissions, projects, db, token); if (error is not null) return error;
        if (!await Allowed(permissions, actor, projectId, "hse.confidential.read", token)) return Results.StatusCode(403);
        var command = await CommandAsync(context, actor, idempotency, "hse.competency.record", request, token); if (command.Replay is not null) return command.Replay;
        var entity = HseCompetencyRecord.Record(request.ClientGeneratedId, actor.TenantId, projectId,
            request.PersonReference, request.InductionDate, request.ValidUntil, request.Competencies,
            request.EvidenceReferences, request.Classification, actor.UserId, clock.UtcNow);
        db.CompetencyRecords.Add(entity); var response = CompetencyResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "HseCompetencyRecord", "HseCompetencyRecorded", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/competencies/{entity.Id}", response);
    }

    private static async Task<IResult> RecordExposureHoursAsync(Guid projectId, RecordExposureHoursRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Hse, "hse.exposure.approve", actor, permissions, projects, db, token); if (error is not null) return error;
        var overlaps = await db.ExposureHours.AnyAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && x.PeriodStart <= request.PeriodEnd && x.PeriodEnd >= request.PeriodStart, token);
        if (overlaps) return Results.Conflict(new { code = "quality_safety.exposure.period.overlap" });
        var command = await CommandAsync(context, actor, idempotency, "hse.exposure.record", request, token); if (command.Replay is not null) return command.Replay;
        var entity = ExposureHoursRecord.Record(request.ClientGeneratedId, actor.TenantId, projectId,
            request.PeriodStart, request.PeriodEnd, request.Hours, request.SourceReference,
            request.EvidenceReferences, actor.UserId, clock.UtcNow); db.ExposureHours.Add(entity);
        var response = ExposureHoursResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "ExposureHoursRecord", "ExposureHoursApproved", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/exposure-hours/{entity.Id}", response);
    }
}
