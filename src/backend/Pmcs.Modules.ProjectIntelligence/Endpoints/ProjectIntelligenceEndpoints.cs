using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pmcs.BuildingBlocks.Application;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.ProjectIntelligence.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Endpoints;

internal static class ProjectIntelligenceEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void MapProjectIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/projects/{projectId:guid}/command-center", GetCommandCenterAsync)
            .WithTags("Project Intelligence");
        endpoints.MapGet("/api/v1/portfolio/command-center", PortfolioCommandCenterEndpoints.GetAsync)
            .WithTags("Portfolio Intelligence");
        endpoints.MapPost("/api/v1/projects/{projectId:guid}/project-state/recalculate", RecalculateAsync)
            .WithTags("Project Intelligence");
    }

    private static async Task<IResult> GetCommandCenterAsync(
        Guid projectId,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        IApprovedDailyFactSource approvedFactSource,
        IAttentionDispositionSource dispositionSource,
        IFinancialStateSource financialStateSource,
        ICommercialStateSource commercialStateSource,
        ProjectIntelligenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "project-state.read",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var latest = await dbContext.ProjectStateSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.AttentionItems)
            .Where(snapshot => snapshot.TenantId == actor.TenantId && snapshot.ProjectId == projectId)
            .OrderByDescending(snapshot => snapshot.CalculatedAt)
            .ThenByDescending(snapshot => snapshot.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var history = await dbContext.ProjectStateSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.TenantId == actor.TenantId && snapshot.ProjectId == projectId)
            .OrderByDescending(snapshot => snapshot.CalculatedAt)
            .ThenByDescending(snapshot => snapshot.Id)
            .Take(14)
            .ToListAsync(cancellationToken);
        var latestApprovedChange = await approvedFactSource.GetLatestApprovedChangeAsync(
            actor.TenantId,
            projectId,
            cancellationToken);
        var isOutdated = latest is not null &&
            (latest.ProjectConfigurationRevision != project.Revision ||
                (latestApprovedChange.HasValue &&
                    (latest.SourceMaxChangedAt is null || latestApprovedChange > latest.SourceMaxChangedAt)));
        var canRecalculate = await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            "project-state.recalculate",
            cancellationToken);
        var canTriage = await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            "attention.triage",
            cancellationToken);
        var canReadFinance = await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            "financial-state.read",
            cancellationToken);
        var financialState = canReadFinance
            ? await financialStateSource.GetCurrentAsync(actor.TenantId, projectId, cancellationToken)
            : null;
        var canReadCommercial = await permissionService.HasProjectPermissionAsync(
            actor.TenantId,
            actor.UserId,
            projectId,
            "commercial-state.read",
            cancellationToken);
        var commercialState = canReadCommercial
            ? await commercialStateSource.GetCurrentAsync(actor.TenantId, projectId, cancellationToken)
            : null;
        var dispositions = latest is null
            ? new Dictionary<Guid, AttentionDispositionRecord>()
            : await dispositionSource.LoadAsync(
                actor.TenantId,
                projectId,
                latest.AttentionItems.Select(item => item.SourceFactId).ToArray(),
                cancellationToken);

        return Results.Ok(new CommandCenterResponse(
            project.Id,
            project.Code,
            project.Name,
            project.TimeZone,
            latest is not null,
            isOutdated,
            canRecalculate,
            canTriage,
            canReadFinance,
            canReadCommercial,
            latest is null ? null : ProjectStateSnapshotResponse.From(latest, dispositions),
            financialState is null ? null : CommandCenterFinancialStateResponse.From(financialState),
            commercialState is null ? null : CommandCenterCommercialStateResponse.From(commercialState),
            BuildCapabilities(project, latest, financialState, commercialState),
            history.Select(snapshot => new ProjectStateTrendPointResponse(
                snapshot.Id,
                snapshot.AsOfDate,
                snapshot.CalculatedAt,
                snapshot.OperationalStatus,
                snapshot.CoveragePercent,
                snapshot.FreshnessStatus,
                snapshot.IssueCount + snapshot.StoppageCount)).ToArray()));
    }

    private static async Task<IResult> RecalculateAsync(
        Guid projectId,
        RecalculateProjectStateRequest request,
        HttpContext httpContext,
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        IApprovedDailyFactSource approvedFactSource,
        ProjectIntelligenceDbContext dbContext,
        IClock clock,
        ITransactionalSideEffectWriter sideEffectWriter,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasProjectPermissionAsync(
                actor.TenantId,
                actor.UserId,
                projectId,
                "project-state.recalculate",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Idempotency-Key is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "idempotency.key.required" });
        }

        var requestHash = RequestHash.Create(JsonSerializer.Serialize(request, SerializerOptions));
        var replay = await idempotencyStore.FindAsync(
            actor.TenantId,
            idempotencyKey,
            "project-state.recalculate",
            requestHash,
            cancellationToken);
        if (replay is not null)
        {
            return Results.Content(replay.ResponseBody, "application/json", Encoding.UTF8, replay.StatusCode);
        }

        var project = await projectDirectory.FindProfileAsync(actor.TenantId, projectId, cancellationToken);
        if (project is null)
        {
            return Results.NotFound(new { code = "project.not_found" });
        }

        var localToday = ResolveLocalDate(clock.UtcNow, project.TimeZone);
        var asOfDate = request.AsOfDate ?? localToday;
        if (asOfDate > localToday)
        {
            throw new DomainRuleException("project_state.as_of.future", "Project state cannot be calculated for a future local date.");
        }

        var source = await approvedFactSource.LoadAsync(
            actor.TenantId,
            projectId,
            asOfDate.AddDays(-(ProjectStateCalculator.AttentionWindowDays - 1)),
            asOfDate,
            cancellationToken);
        var calculation = ProjectStateCalculator.Calculate(project, source, asOfDate, clock.UtcNow);
        var snapshot = ProjectStateSnapshot.Create(Guid.NewGuid(), calculation);
        dbContext.ProjectStateSnapshots.Add(snapshot);

        var response = ProjectStateSnapshotResponse.From(snapshot);
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
                    "ProjectStateCalculated",
                    "ProjectStateSnapshot",
                    snapshot.Id.ToString(),
                    clock.UtcNow,
                    new Dictionary<string, object?>
                    {
                        ["calculationVersion"] = snapshot.CalculationVersion,
                        ["asOfDate"] = snapshot.AsOfDate,
                        ["operationalStatus"] = snapshot.OperationalStatus.ToString(),
                        ["coveragePercent"] = snapshot.CoveragePercent,
                        ["assessmentScope"] = snapshot.AssessmentScope.ToString(),
                        ["isPartial"] = snapshot.IsPartial
                    },
                    httpContext.TraceIdentifier),
                new OutboxEnvelope(
                    Guid.NewGuid(),
                    actor.TenantId,
                    projectId,
                    "ProjectIntelligence.ProjectStateCalculated",
                    1,
                    clock.UtcNow,
                    responseJson,
                    httpContext.TraceIdentifier),
                new IdempotencyReceipt(
                    actor.TenantId,
                    idempotencyKey,
                    "project-state.recalculate",
                    requestHash,
                    StatusCodes.Status201Created,
                    responseJson,
                    clock.UtcNow,
                    clock.UtcNow.AddDays(7))),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created(
            $"/api/v1/projects/{projectId}/command-center?snapshotId={snapshot.Id}",
            response);
    }

    internal static IReadOnlyCollection<ProjectCapabilityResponse> BuildCapabilities(
        ProjectControlProfile project,
        ProjectStateSnapshot? snapshot,
        FinancialStateRecord? financialState,
        CommercialStateRecord? commercialState) =>
    [
        new(
            "operations",
            "Field Operations",
            ProjectFeatureState.Active,
            snapshot?.ApprovedReportDays > 0 ? CapabilityMetricState.Available : CapabilityMetricState.NoData,
            IncludedInAssessment: true),
        BuildCapability(
            "contract",
            "Contract",
            project.Contract,
            commercialState?.ContractState == CommercialMetricState.Available
                ? CapabilityMetricState.Available
                : null),
        BuildCapability(
            "procurement",
            "Procurement",
            project.Procurement,
            commercialState?.ProcurementState == CommercialMetricState.Available
                ? CapabilityMetricState.Available
                : null),
        BuildCapability("planning", "Planning / WBS", project.Planning),
        new(
            "calendar",
            "Project Calendar",
            project.Calendar.State == ProjectCalendarConfigurationState.Configured
                ? ProjectFeatureState.Active
                : ProjectFeatureState.NotConfigured,
            project.Calendar.State == ProjectCalendarConfigurationState.Configured
                ? CapabilityMetricState.Available
                : CapabilityMetricState.NotApplicable,
            IncludedInAssessment: project.Calendar.State == ProjectCalendarConfigurationState.Configured),
        BuildCapability(
            "budget",
            "Budget",
            financialState?.BudgetComparisonState == BudgetComparisonState.Available
                ? ProjectFeatureState.Active
                : project.Budget,
            financialState?.BudgetComparisonState == BudgetComparisonState.Available
                ? CapabilityMetricState.Available
                : null),
        BuildCapability(
            "finance",
            "Finance Lite",
            project.Finance,
            financialState?.Status == FinancialStateStatus.Available
                ? CapabilityMetricState.Available
                : null),
        BuildCapability("quality", "Quality", project.Quality),
        BuildCapability("hse", "HSE", project.Hse)
    ];

    private static ProjectCapabilityResponse BuildCapability(
        string key,
        string label,
        ProjectFeatureState state,
        CapabilityMetricState? metricState = null) => new(
        key,
        label,
        state,
        metricState ?? (state is ProjectFeatureState.NotConfigured or ProjectFeatureState.NotEnabled
            ? CapabilityMetricState.NotApplicable
            : CapabilityMetricState.NoData),
        IncludedInAssessment: false);

    internal static DateOnly ResolveLocalDate(DateTimeOffset now, string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new DomainRuleException("project.time_zone.unavailable", exception.Message);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new DomainRuleException("project.time_zone.invalid", exception.Message);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
