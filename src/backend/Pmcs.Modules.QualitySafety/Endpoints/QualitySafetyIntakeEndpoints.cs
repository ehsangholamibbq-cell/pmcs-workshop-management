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
    private static async Task<IResult> CaptureIntakeAsync(Guid projectId, CaptureIntakeRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        var quality = request.Kind is IntakeKind.QualityObservation or IntakeKind.Defect;
        var permission = quality ? "quality.intake.capture" : "hse.intake.capture";
        if (!await Allowed(permissions, actor, projectId, permission, token)) return Results.StatusCode(403);
        var area = await RequireAreaAsync(projectId, quality ? ControlArea.Quality : ControlArea.Hse,
            actor, projects, db, false, token);
        if (area.Error is not null) return area.Error;
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.intake.capture", request, token);
        if (command.Replay is not null) return command.Replay;
        var intake = QualitySafetyIntake.Capture(request.ClientGeneratedId, actor.TenantId, projectId,
            request.Kind, request.ObservedAt, request.Location, request.Facts, request.InitialSeverity,
            request.ImmediateAction, request.EvidenceReferences, request.Classification, actor.UserId, clock.UtcNow);
        db.Intakes.Add(intake); var response = IntakeResponse.From(intake);
        await PersistAsync(db, context, actor, projectId, intake.Id, "QualitySafetyIntake", "IntakeCaptured",
            response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/intakes/{intake.Id}", response);
    }

    private static async Task<IResult> BeginTriageAsync(Guid projectId, Guid intakeId, TriageIntakeRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var intake = await FindIntake(db, actor, projectId, intakeId, token);
        if (intake is null) return actor.IsAuthenticated ? Results.NotFound() : Results.Unauthorized();
        var permission = intake.IsQuality ? "quality.intake.triage" : "hse.intake.triage";
        if (!await Allowed(permissions, actor, projectId, permission, token)) return Results.StatusCode(403);
        if (intake.Revision != request.BaseRevision) return Conflict("quality_safety.intake.revision.conflict", intake.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.intake.triage", request, token);
        if (command.Replay is not null) return command.Replay;
        intake.BeginTriage(request.BaseRevision, actor.UserId, clock.UtcNow); var response = IntakeResponse.From(intake);
        await PersistAsync(db, context, actor, projectId, intake.Id, "QualitySafetyIntake", "IntakeTriageStarted",
            response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> ResolveIntakeAsync(Guid projectId, Guid intakeId, ResolveIntakeRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var intake = await FindIntake(db, actor, projectId, intakeId, token);
        if (intake is null) return actor.IsAuthenticated ? Results.NotFound() : Results.Unauthorized();
        var permission = intake.IsQuality ? "quality.intake.triage" : "hse.intake.triage";
        if (!await Allowed(permissions, actor, projectId, permission, token)) return Results.StatusCode(403);
        if (intake.Revision != request.BaseRevision) return Conflict("quality_safety.intake.revision.conflict", intake.Revision);
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.intake.resolve", request, token);
        if (command.Replay is not null) return command.Replay;
        intake.ResolveWithoutFormalRecord(request.BaseRevision, request.RetainAsGeneralIssue, request.Note, actor.UserId, clock.UtcNow);
        var response = IntakeResponse.From(intake);
        await PersistAsync(db, context, actor, projectId, intake.Id, "QualitySafetyIntake", "IntakeResolved",
            response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<IResult> ConvertIntakeAsync(Guid projectId, Guid intakeId, ConvertIntakeRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        var intake = await FindIntake(db, actor, projectId, intakeId, token);
        if (intake is null) return actor.IsAuthenticated ? Results.NotFound() : Results.Unauthorized();
        var permission = intake.IsQuality ? "quality.intake.triage" : "hse.intake.triage";
        if (!await Allowed(permissions, actor, projectId, permission, token)) return Results.StatusCode(403);
        if (intake.Revision != request.BaseRevision) return Conflict("quality_safety.intake.revision.conflict", intake.Revision);
        var area = await RequireAreaAsync(projectId, intake.IsQuality ? ControlArea.Quality : ControlArea.Hse,
            actor, projects, db, true, token); if (area.Error is not null) return area.Error;
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.intake.convert", request, token);
        if (command.Replay is not null) return command.Replay;

        var targetId = request.ConversionType is IntakeConversionType.QualityObservation or IntakeConversionType.HseObservation
            ? intake.Id
            : request.ClientGeneratedRecordId;
        switch (request.ConversionType)
        {
            case IntakeConversionType.Defect:
                db.Defects.Add(DefectRecord.Create(request.ClientGeneratedRecordId, actor.TenantId, projectId, intake.Id,
                    request.Title ?? intake.Facts, intake.Location, actor.UserId, clock.UtcNow)); break;
            case IntakeConversionType.NonConformance:
                db.NonConformances.Add(NonConformanceRecord.Create(request.ClientGeneratedRecordId, actor.TenantId,
                    projectId, intake.Id, null, null, null, null, null, null, request.Title ?? intake.Facts,
                    request.Requirement ?? "نیازمندی در مرحله بررسی رسمی تعیین می‌شود", intake.Facts, actor.UserId, clock.UtcNow)); break;
            case IntakeConversionType.Incident:
                var matrix = area.Configuration!.HseMatrixVersionId;
                if (!matrix.HasValue) return Results.UnprocessableEntity(new { code = "quality_safety.incident.matrix.required" });
                db.Incidents.Add(SafetyIncident.Report(request.ClientGeneratedRecordId, actor.TenantId, projectId,
                    intake.Id, intake.ObservedAt, intake.Location, intake.Facts,
                    intake.InitialSeverity == InitialSeverity.Unassessed ? InitialSeverity.Low : intake.InitialSeverity,
                    matrix.Value, intake.Classification == DataClassification.GeneralProject ? DataClassification.ConfidentialHse : intake.Classification,
                    actor.UserId, clock.UtcNow)); break;
            case IntakeConversionType.QualityObservation:
            case IntakeConversionType.HseObservation:
                break;
            default: return Results.UnprocessableEntity(new { code = "quality_safety.intake.conversion.unsupported" });
        }
        intake.Convert(request.BaseRevision, request.ConversionType, targetId,
            request.Note, actor.UserId, clock.UtcNow); var response = IntakeResponse.From(intake);
        await PersistAsync(db, context, actor, projectId, intake.Id, "QualitySafetyIntake", "IntakeConverted",
            response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static Task<QualitySafetyIntake?> FindIntake(QualitySafetyDbContext db, ICurrentActor actor,
        Guid projectId, Guid intakeId, CancellationToken token) => actor.IsAuthenticated
        ? db.Intakes.SingleOrDefaultAsync(x => x.Id == intakeId && x.TenantId == actor.TenantId && x.ProjectId == projectId, token)
        : Task.FromResult<QualitySafetyIntake?>(null);
}
