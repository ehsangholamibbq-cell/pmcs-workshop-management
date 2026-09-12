using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.QualitySafety.Domain;
using Pmcs.Modules.QualitySafety.Persistence;
using static Pmcs.Modules.QualitySafety.Endpoints.QualitySafetyEndpointSupport;

namespace Pmcs.Modules.QualitySafety.Endpoints;

internal static partial class QualitySafetyEndpoints
{
    private static async Task<IResult> GetStateAsync(Guid projectId, ICurrentActor actor,
        IProjectPermissionService permissions, IProjectDirectory projects, QualitySafetyDbContext db,
        IClock clock, CancellationToken token)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        var canReadQuality = await Allowed(permissions, actor, projectId, "quality.read", token);
        var canReadHse = await Allowed(permissions, actor, projectId, "hse.read", token);
        if (!canReadQuality && !canReadHse) return Results.StatusCode(403);
        var profile = await projects.FindProfileAsync(actor.TenantId, projectId, token);
        if (profile is null) return Results.NotFound(new { code = "project.not_found" });

        var configuration = await db.Configurations.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == actor.TenantId && x.ProjectId == projectId, token);
        var canReadConfidentialHse = canReadHse && await Allowed(permissions, actor, projectId, "hse.confidential.read", token);

        var matrices = await db.RiskMatrices.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId &&
            ((canReadQuality && x.Area == ControlArea.Quality) || (canReadHse && x.Area == ControlArea.Hse)))
            .OrderByDescending(x => x.Version).Take(40).ToListAsync(token);
        var intakes = await db.Intakes.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId &&
            ((canReadQuality && (x.Kind == IntakeKind.QualityObservation || x.Kind == IntakeKind.Defect)) ||
             (canReadHse && x.Kind != IntakeKind.QualityObservation && x.Kind != IntakeKind.Defect &&
              (canReadConfidentialHse || x.Classification == DataClassification.GeneralProject))))
            .OrderByDescending(x => x.ObservedAt).Take(100).ToListAsync(token);
        var inspections = canReadQuality ? await db.Inspections.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.RequestedFor).Take(100).ToListAsync(token) : [];
        var ncrs = canReadQuality ? await db.NonConformances.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(token) : [];
        var defects = canReadQuality ? await db.Defects.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(token) : [];
        var incidents = canReadConfidentialHse ? await db.Incidents.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.OccurredAt).Take(100).ToListAsync(token) : [];
        var actionQuery = db.CorrectiveActions.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId &&
            ((canReadQuality && x.SourceArea == ControlArea.Quality) ||
             (canReadHse && x.SourceArea == ControlArea.Hse &&
              (canReadConfidentialHse || x.SourceRecordType != "Incident"))));
        var actions = await actionQuery
            .OrderBy(x => x.Status == CorrectiveActionStatus.Closed || x.Status == CorrectiveActionStatus.Cancelled)
            .ThenBy(x => x.DueDate).Take(150).ToListAsync(token);
        var permits = canReadHse ? await db.Permits.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.ValidFrom).Take(100).ToListAsync(token) : [];
        var talks = canReadHse ? await db.ToolboxTalks.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.HeldAt).Take(100).ToListAsync(token) : [];
        var plans = canReadQuality ? await db.InspectionTestPlans.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.EffectiveFrom).Take(100).ToListAsync(token) : [];
        var checklists = canReadQuality ? await db.ChecklistTemplates.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.EffectiveFrom).Take(100).ToListAsync(token) : [];
        var tests = canReadQuality ? await db.TestRecords.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.TestedAt).Take(100).ToListAsync(token) : [];
        var competencies = canReadConfidentialHse ? await db.CompetencyRecords.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.InductionDate).Take(100).ToListAsync(token) : [];
        var exposure = canReadHse ? await db.ExposureHours.AsNoTracking().Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
            .OrderByDescending(x => x.PeriodEnd).Take(120).ToListAsync(token) : [];
        var today = LocalDate(clock.UtcNow, profile.TimeZone);
        var qualityHasData = intakes.Any(x => x.IsQuality) || inspections.Count != 0 || ncrs.Count != 0 ||
            defects.Count != 0 || plans.Count != 0 || checklists.Count != 0 || tests.Count != 0;
        var hseHasData = intakes.Any(x => x.IsHse) || incidents.Count != 0 || permits.Count != 0 ||
            talks.Count != 0 || competencies.Count != 0 || exposure.Count != 0;
        var qualityState = CapabilityState(profile.Quality, configuration?.QualityMode == QualityOperatingMode.Disabled,
            configuration?.QualityReady, canReadQuality, qualityHasData);
        var hseState = CapabilityState(profile.Hse, configuration?.HseMode == HseOperatingMode.Disabled,
            configuration?.HseReady, canReadHse, hseHasData);
        var openQuality = canReadQuality
            ? await db.NonConformances.CountAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Status != NcrStatus.Closed, token) +
              await db.Defects.CountAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Status != DefectStatus.Closed, token)
            : 0;
        int? openHse = canReadConfidentialHse
            ? await db.Incidents.CountAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Status != IncidentStatus.Closed, token)
            : null;
        var exposureHours = canReadConfidentialHse
            ? await db.ExposureHours.Where(x => x.TenantId == actor.TenantId && x.ProjectId == projectId)
                .SumAsync(x => (decimal?)x.Hours, token) ?? 0m
            : 0m;
        var totalReportedIncidents = canReadConfidentialHse
            ? await db.Incidents.CountAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId, token)
            : 0;
        decimal? reportedIncidentFrequency = canReadConfidentialHse && exposureHours > 0
            ? decimal.Round(totalReportedIncidents * 200_000m / exposureHours, 4, MidpointRounding.AwayFromZero)
            : null;
        var overdue = await actionQuery.CountAsync(x =>
            x.Status != CorrectiveActionStatus.Closed && x.Status != CorrectiveActionStatus.Cancelled &&
            (x.ExtendedDueDate ?? x.DueDate) < today, token);
        return Results.Ok(new QualitySafetyStateResponse(qualityState, hseState,
            configuration is null ? null : ConfigurationResponse.From(configuration),
            matrices.Select(RiskMatrixResponse.From).ToArray(), intakes.Select(IntakeResponse.From).ToArray(),
            inspections.Select(InspectionResponse.From).ToArray(), ncrs.Select(NcrResponse.From).ToArray(),
            defects.Select(DefectResponse.From).ToArray(), incidents.Select(IncidentResponse.From).ToArray(),
            actions.Select(CorrectiveActionResponse.From).ToArray(), permits.Select(PermitResponse.From).ToArray(),
            talks.Select(ToolboxTalkResponse.From).ToArray(), plans.Select(InspectionTestPlanResponse.From).ToArray(),
            checklists.Select(ChecklistTemplateResponse.From).ToArray(), tests.Select(QualityTestResponse.From).ToArray(),
            competencies.Select(CompetencyResponse.From).ToArray(), exposure.Select(ExposureHoursResponse.From).ToArray(),
            openQuality, openHse, overdue, reportedIncidentFrequency.HasValue, reportedIncidentFrequency,
            canReadHse && !reportedIncidentFrequency.HasValue
                ? "نرخ حادثه فقط پس از ثبت ساعات مواجهه معتبر و دسترسی مجاز محاسبه می‌شود."
                : null));
    }

    private static string CapabilityState(ProjectFeatureState feature, bool? explicitlyDisabled, bool? ready,
        bool visible, bool hasData)
    {
        if (!visible) return "Hidden";
        if (feature == ProjectFeatureState.NotEnabled || explicitlyDisabled == true) return "NotEnabled";
        if (feature == ProjectFeatureState.Suspended) return "Suspended";
        if (ready != true) return "SetupRequired";
        return hasData ? "Available" : "NoData";
    }

    private static async Task<IResult> CreateMatrixAsync(Guid projectId, CreateRiskMatrixRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await Allowed(permissions, actor, projectId, "quality_safety.configure", token)) return Results.StatusCode(403);
        var project = await projects.FindProfileAsync(actor.TenantId, projectId, token);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        var feature = request.Area == ControlArea.Quality ? project.Quality : project.Hse;
        if (feature == ProjectFeatureState.NotEnabled) return Results.UnprocessableEntity(new { code = "quality_safety.feature.not_enabled" });
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.matrix.create", request, token);
        if (command.Replay is not null) return command.Replay;
        if (await db.RiskMatrices.AnyAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Area == request.Area && x.Version == request.Version, token))
            return Results.Conflict(new { code = "quality_safety.matrix.version.exists" });
        var entity = RiskMatrixVersion.Create(request.ClientGeneratedId, actor.TenantId, projectId, request.Area,
            request.Version, request.Title, request.DefinitionJson, request.EffectiveFrom, actor.UserId, clock.UtcNow);
        db.RiskMatrices.Add(entity); var response = RiskMatrixResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "RiskMatrixVersion", "RiskMatrixVersionCreated",
            response, command.Command!, 201, effects, clock, token);
        return Results.Created($"/api/v1/projects/{projectId}/quality-safety/matrices/{entity.Id}", response);
    }

    private static async Task<IResult> ConfigureAsync(Guid projectId, ConfigureQualitySafetyRequest request,
        HttpContext context, ICurrentActor actor, IProjectPermissionService permissions, IProjectDirectory projects,
        QualitySafetyDbContext db, IClock clock, ITransactionalSideEffectWriter effects,
        IIdempotencyStore idempotency, CancellationToken token)
    {
        if (!actor.IsAuthenticated) return Results.Unauthorized();
        if (!await Allowed(permissions, actor, projectId, "quality_safety.configure", token)) return Results.StatusCode(403);
        var project = await projects.FindProfileAsync(actor.TenantId, projectId, token);
        if (project is null) return Results.NotFound(new { code = "project.not_found" });
        if (project.Quality == ProjectFeatureState.NotEnabled && request.QualityMode != QualityOperatingMode.Disabled ||
            project.Hse == ProjectFeatureState.NotEnabled && request.HseMode != HseOperatingMode.Disabled)
            return Results.UnprocessableEntity(new { code = "quality_safety.configuration.project_capability_mismatch" });
        if (!await MatrixMatches(db, actor, projectId, request.QualityMatrixVersionId, ControlArea.Quality, token) ||
            !await MatrixMatches(db, actor, projectId, request.HseMatrixVersionId, ControlArea.Hse, token))
            return Results.UnprocessableEntity(new { code = "quality_safety.configuration.matrix.invalid" });
        var command = await CommandAsync(context, actor, idempotency, "quality-safety.configuration.put", request, token);
        if (command.Replay is not null) return command.Replay;
        var entity = await db.Configurations.SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.ProjectId == projectId, token);
        if (entity is null)
        {
            if (request.BaseRevision.HasValue) return Results.Conflict(new { code = "quality_safety.configuration.not_created" });
            entity = QualitySafetyConfiguration.Create(Guid.NewGuid(), actor.TenantId, projectId, request.QualityMode,
                request.HseMode, request.QualityOwnerUserId, request.HseOwnerUserId, request.QualityMatrixVersionId,
                request.HseMatrixVersionId, request.WorkflowAndSlaDefined, request.TemplatesDefined,
                request.EvidenceAndClosureRulesDefined, actor.UserId, clock.UtcNow); db.Configurations.Add(entity);
        }
        else
        {
            if (!request.BaseRevision.HasValue || entity.Revision != request.BaseRevision.Value)
                return Conflict("quality_safety.configuration.revision.conflict", entity.Revision);
            entity.Reconfigure(request.BaseRevision.Value, request.QualityMode, request.HseMode,
                request.QualityOwnerUserId, request.HseOwnerUserId, request.QualityMatrixVersionId,
                request.HseMatrixVersionId, request.WorkflowAndSlaDefined, request.TemplatesDefined,
                request.EvidenceAndClosureRulesDefined, actor.UserId, clock.UtcNow);
        }
        var response = ConfigurationResponse.From(entity);
        await PersistAsync(db, context, actor, projectId, entity.Id, "QualitySafetyConfiguration", "ConfigurationChanged",
            response, command.Command!, 200, effects, clock, token); return Results.Ok(response);
    }

    private static async Task<bool> MatrixMatches(QualitySafetyDbContext db, ICurrentActor actor,
        Guid projectId, Guid? id, ControlArea area, CancellationToken token) => !id.HasValue ||
        await db.RiskMatrices.AnyAsync(x => x.Id == id && x.TenantId == actor.TenantId && x.ProjectId == projectId && x.Area == area, token);
}
