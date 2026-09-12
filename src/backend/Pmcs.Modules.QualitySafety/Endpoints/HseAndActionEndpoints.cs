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
    private static async Task<IResult> ReportIncidentAsync(Guid projectId, ReportIncidentRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Hse, "hse.incidents.report", actor, permissions, projects, db, token); if (error is not null) return error;
        var validMatrix = await db.RiskMatrices.AnyAsync(x => x.Id == request.MatrixVersionId && x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Area == ControlArea.Hse, token);
        if (!validMatrix) return Results.UnprocessableEntity(new { code = "quality_safety.incident.matrix.invalid" });
        var command = await CommandAsync(context, actor, idempotency, "hse.incident.report", request, token); if (command.Replay is not null) return command.Replay;
        var entity = SafetyIncident.Report(request.ClientGeneratedId, actor.TenantId, projectId, request.SourceIntakeId,
            request.OccurredAt, request.Location, request.Facts, request.PreliminarySeverity,
            request.MatrixVersionId, request.Classification, actor.UserId, clock.UtcNow);
        db.Incidents.Add(entity); var response = IncidentResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "SafetyIncident", "IncidentReported", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/incidents/{entity.Id}", response);
    }

    private static async Task<IResult> TransitionIncidentAsync(Guid projectId, Guid incidentId,
        TransitionIncidentRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        var permission = request.TargetStatus == IncidentStatus.Closed ? "hse.incidents.verify" : "hse.incidents.investigate";
        var error = await AuthorizeArea(projectId, ControlArea.Hse, permission, actor, permissions, projects, db, token); if (error is not null) return error;
        if (!await Allowed(permissions, actor, projectId, "hse.confidential.read", token)) return Results.StatusCode(403);
        var entity = await db.Incidents.SingleOrDefaultAsync(x => x.Id == incidentId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.incident.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "hse.incident.transition", request, token); if (command.Replay is not null) return command.Replay;
        entity.Transition(request.BaseRevision, request.TargetStatus, request.FinalSeverity, request.RootCauseStatus,
            request.RootCause, request.ClosureEvidence, clock.UtcNow); var response = IncidentResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "SafetyIncident", "IncidentTransitioned", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> CreateActionAsync(Guid projectId, CreateCorrectiveActionRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var permission = request.SourceArea == ControlArea.Quality ? "quality.actions.create" : "hse.actions.create";
        var error = await AuthorizeArea(projectId, request.SourceArea, permission, actor, permissions, projects, db, token); if (error is not null) return error;
        var project = await projects.FindProfileAsync(actor.TenantId, projectId, token);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        if (request.DueDate < LocalDate(clock.UtcNow, project.TimeZone))
            return Results.UnprocessableEntity(new { code = "quality_safety.action.due_date.in_past" });
        if (!await SourceExists(db, actor, projectId, request.SourceArea, request.SourceRecordType, request.SourceRecordId, token))
            return Results.UnprocessableEntity(new { code = "quality_safety.action.source.invalid" });
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.action.create", request, token); if (command.Replay is not null) return command.Replay;
        var entity = CorrectiveAction.Create(request.ClientGeneratedId, actor.TenantId, projectId,
            request.SourceArea, request.SourceRecordType, request.SourceRecordId, request.Kind, request.Title,
            request.OwnerUserId, request.ResponsibleParty, request.DueDate, request.SuccessCriteria, actor.UserId, clock.UtcNow);
        db.CorrectiveActions.Add(entity); var response = CorrectiveActionResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "CorrectiveAction", "CorrectiveActionCreated", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/corrective-actions/{entity.Id}", response);
    }

    private static async Task<IResult> TransitionActionAsync(Guid projectId, Guid actionId,
        TransitionCorrectiveActionRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, QualitySafetyDbContext db, IClock clock,
        ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        var entity = await db.CorrectiveActions.SingleOrDefaultAsync(x => x.Id == actionId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        var permissionPrefix = entity.SourceArea == ControlArea.Quality ? "quality.actions" : "hse.actions";
        var permission = request.TargetStatus is CorrectiveActionStatus.Verified or CorrectiveActionStatus.Closed
            ? $"{permissionPrefix}.verify" : $"{permissionPrefix}.update";
        if (!await Allowed(permissions, actor, projectId, permission, token)) return Results.StatusCode(403);
        if (entity.SourceArea == ControlArea.Hse && entity.SourceRecordType.Equals("Incident", StringComparison.OrdinalIgnoreCase) &&
            !await Allowed(permissions, actor, projectId, "hse.confidential.read", token)) return Results.StatusCode(403);
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.action.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.action.transition", request, token); if (command.Replay is not null) return command.Replay;
        entity.Transition(request.BaseRevision, request.TargetStatus, request.EvidenceReferences, actor.UserId, clock.UtcNow); var response = CorrectiveActionResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "CorrectiveAction", "CorrectiveActionTransitioned", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> ExtendActionAsync(Guid projectId, Guid actionId,
        ExtendCorrectiveActionRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db, IClock clock,
        ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        var entity = await db.CorrectiveActions.SingleOrDefaultAsync(x => x.Id == actionId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        var project = await projects.FindProfileAsync(actor.TenantId, projectId, token);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        if (request.ExtendedDueDate < LocalDate(clock.UtcNow, project.TimeZone))
            return Results.UnprocessableEntity(new { code = "quality_safety.action.due_date.in_past" });
        var permission = entity.SourceArea == ControlArea.Quality ? "quality.actions.extend" : "hse.actions.extend";
        if (!await Allowed(permissions, actor, projectId, permission, token)) return Results.StatusCode(403);
        if (entity.SourceArea == ControlArea.Hse && entity.SourceRecordType.Equals("Incident", StringComparison.OrdinalIgnoreCase) &&
            !await Allowed(permissions, actor, projectId, "hse.confidential.read", token)) return Results.StatusCode(403);
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.action.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.action.extend", request, token); if (command.Replay is not null) return command.Replay;
        entity.Extend(request.BaseRevision, request.ExtendedDueDate, request.Reason, actor.UserId); var response = CorrectiveActionResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "CorrectiveAction", "CorrectiveActionDueDateExtended", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> CreatePermitAsync(Guid projectId, CreatePermitRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Hse, "hse.permits.create", actor, permissions, projects, db, token); if (error is not null) return error;
        var command = await CommandAsync(context, actor, idempotency, "hse.permit.create", request, token); if (command.Replay is not null) return command.Replay;
        var entity = PermitToWork.Create(request.ClientGeneratedId, actor.TenantId, projectId, request.WorkDescription,
            request.Location, request.ValidFrom, request.ValidTo, request.Hazards, request.Controls, actor.UserId, clock.UtcNow);
        db.Permits.Add(entity); var response = PermitResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "PermitToWork", "PermitCreated", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/permits/{entity.Id}", response);
    }

    private static async Task<IResult> TransitionPermitAsync(Guid projectId, Guid permitId,
        TransitionPermitRequest request, HttpContext context, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db,
        IClock clock, ITransactionalSideEffectWriter effects, IIdempotencyStore idempotency, CancellationToken token)
    {
        var permission = request.TargetStatus is PermitStatus.Approved or PermitStatus.Active ? "hse.permits.issue" : "hse.permits.manage";
        var error = await AuthorizeArea(projectId, ControlArea.Hse, permission, actor, permissions, projects, db, token); if (error is not null) return error;
        var entity = await db.Permits.SingleOrDefaultAsync(x => x.Id == permitId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token); if (entity is null) return Results.NotFound();
        if (entity.Revision != request.BaseRevision) return Conflict("quality_safety.permit.revision.conflict", entity.Revision);
        var command = await CommandAsync(context, actor, idempotency, "hse.permit.transition", request, token); if (command.Replay is not null) return command.Replay;
        entity.Transition(request.BaseRevision, request.TargetStatus, actor.UserId, clock.UtcNow); var response = PermitResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "PermitToWork", "PermitTransitioned", response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> RecordToolboxTalkAsync(Guid projectId, RecordToolboxTalkRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var error = await AuthorizeArea(projectId, ControlArea.Hse, "hse.toolbox.capture", actor, permissions, projects, db, token); if (error is not null) return error;
        var command = await CommandAsync(context, actor, idempotency, "hse.toolbox.record", request, token); if (command.Replay is not null) return command.Replay;
        var entity = ToolboxTalk.Record(request.ClientGeneratedId, actor.TenantId, projectId, request.Topic,
            request.HeldAt, request.Location, request.Attendees, request.EvidenceReferences, actor.UserId, clock.UtcNow);
        db.ToolboxTalks.Add(entity); var response = ToolboxTalkResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "ToolboxTalk", "ToolboxTalkRecorded", response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/toolbox-talks/{entity.Id}", response);
    }

    private static async Task<bool> SourceExists(QualitySafetyDbContext db, ICurrentActor actor, Guid projectId,
        ControlArea area, string type, Guid id, CancellationToken token) => area switch
    {
        ControlArea.Quality when type.Equals("NCR", StringComparison.OrdinalIgnoreCase) => await db.NonConformances.AnyAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId, token),
        ControlArea.Quality when type.Equals("Defect", StringComparison.OrdinalIgnoreCase) => await db.Defects.AnyAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId, token),
        ControlArea.Quality when type.Equals("Inspection", StringComparison.OrdinalIgnoreCase) => await db.Inspections.AnyAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId, token),
        ControlArea.Hse when type.Equals("Incident", StringComparison.OrdinalIgnoreCase) => await db.Incidents.AnyAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId, token),
        ControlArea.Hse when type.Equals("Intake", StringComparison.OrdinalIgnoreCase) => await db.Intakes.AnyAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Kind != IntakeKind.QualityObservation && x.Kind != IntakeKind.Defect, token),
        _ => false
    };

    private static DateOnly LocalDate(DateTimeOffset instant, string timeZone)
    {
        try { return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime); }
        catch (TimeZoneNotFoundException) { return DateOnly.FromDateTime(instant.UtcDateTime); }
        catch (InvalidTimeZoneException) { return DateOnly.FromDateTime(instant.UtcDateTime); }
    }
}
