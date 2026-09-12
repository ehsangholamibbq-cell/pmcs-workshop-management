using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.IdentityAccess.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.ProjectIntelligence.Persistence;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Endpoints;

internal static class PortfolioCommandCenterEndpoints
{
    internal static async Task<IResult> GetAsync(
        ICurrentActor actor,
        IProjectPermissionService permissionService,
        IProjectDirectory projectDirectory,
        IApprovedDailyFactSource approvedFactSource,
        IProjectLeadershipDirectory leadershipDirectory,
        IPortfolioActionSource actionSource,
        IFinancialStateSource financialStateSource,
        ICommercialStateSource commercialStateSource,
        ProjectIntelligenceDbContext dbContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return Results.Unauthorized();
        }

        if (!await permissionService.HasTenantPermissionAsync(
                actor.TenantId,
                actor.UserId,
                "portfolio.read",
                cancellationToken))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var stateScope = await permissionService.GetProjectScopeAsync(
            actor.TenantId,
            actor.UserId,
            "project-state.read",
            cancellationToken);
        var projects = await projectDirectory.ListProfilesAsync(
            actor.TenantId,
            stateScope.AllProjects ? null : stateScope.ProjectIds,
            cancellationToken);
        var projectIds = projects.Select(project => project.Id).ToArray();

        var latestSnapshots = await LoadLatestSnapshotsAsync(
            dbContext,
            actor.TenantId,
            projectIds,
            cancellationToken);
        var approvedChanges = await approvedFactSource.GetLatestApprovedChangesAsync(
            actor.TenantId,
            projectIds,
            cancellationToken);
        var leaders = await leadershipDirectory.ListProjectManagersAsync(
            actor.TenantId,
            projectIds,
            cancellationToken);

        var financeScope = await permissionService.GetProjectScopeAsync(
            actor.TenantId,
            actor.UserId,
            "financial-state.read",
            cancellationToken);
        var commercialScope = await permissionService.GetProjectScopeAsync(
            actor.TenantId,
            actor.UserId,
            "commercial-state.read",
            cancellationToken);
        var actionScope = await permissionService.GetProjectScopeAsync(
            actor.TenantId,
            actor.UserId,
            "actions.read",
            cancellationToken);

        var financeProjects = projects.Where(project => Includes(financeScope, project.Id)).ToArray();
        var commercialProjects = projects.Where(project => Includes(commercialScope, project.Id)).ToArray();
        var actionProjectIds = projectIds.Where(projectId => Includes(actionScope, projectId)).ToArray();
        var financialStates = await financialStateSource.GetPortfolioAsync(
            actor.TenantId,
            financeProjects,
            cancellationToken);
        var commercialStates = await commercialStateSource.GetPortfolioAsync(
            actor.TenantId,
            commercialProjects,
            cancellationToken);
        var actions = await actionSource.ListOpenAsync(actor.TenantId, actionProjectIds, cancellationToken);

        var managerNames = leaders
            .GroupBy(item => item.ProjectId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group
                    .Select(item => item.DisplayName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        var actionsByProject = actions
            .GroupBy(item => item.ProjectId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var localDates = projects.ToDictionary(
            project => project.Id,
            project => ProjectIntelligenceEndpoints.ResolveLocalDate(clock.UtcNow, project.TimeZone));

        var projectResponses = projects
            .Select(project => BuildProjectResponse(
                project,
                latestSnapshots.GetValueOrDefault(project.Id),
                approvedChanges.GetValueOrDefault(project.Id),
                managerNames.GetValueOrDefault(project.Id) ?? [],
                Includes(financeScope, project.Id),
                financialStates.GetValueOrDefault(project.Id),
                Includes(commercialScope, project.Id),
                commercialStates.GetValueOrDefault(project.Id),
                Includes(actionScope, project.Id),
                actionsByProject.GetValueOrDefault(project.Id) ?? [],
                localDates[project.Id]))
            .OrderByDescending(item => OperationalRank(item.Operational.Status))
            .ThenByDescending(item => item.Actions.OverdueCount ?? -1)
            .ThenBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var responseIndex = projectResponses.ToDictionary(item => item.ProjectId);
        var overview = PortfolioOverviewCalculator.Calculate(projects.Select(project =>
        {
            var response = responseIndex[project.Id];
            var financialState = financialStates.GetValueOrDefault(project.Id);
            var commercialState = commercialStates.GetValueOrDefault(project.Id);
            return new PortfolioProjectOverviewInput(
                project.Id,
                project.Status,
                response.Operational.HasSnapshot,
                response.Operational.Status,
                response.Operational.FreshnessStatus,
                response.Operational.IsOutdated,
                response.Actions.IsVisible,
                response.Actions.OpenCount ?? 0,
                response.Actions.OverdueCount ?? 0,
                financialState,
                commercialState,
                LatestSnapshotAt(response));
        }).ToArray());

        var projectIndex = projects.ToDictionary(item => item.Id);
        var actionExceptions = actions
            .Where(action => projectIndex.ContainsKey(action.ProjectId))
            .Select(action =>
            {
                var project = projectIndex[action.ProjectId];
                return new PortfolioActionExceptionResponse(
                    action.Id,
                    project.Id,
                    project.Code,
                    project.Name,
                    action.Title,
                    action.AssigneeUserId,
                    action.AssigneeDisplayName,
                    action.DueDate,
                    action.DueDate < localDates[project.Id],
                    action.Priority,
                    action.Status,
                    action.Revision);
            })
            .OrderByDescending(item => item.IsOverdue)
            .ThenByDescending(item => ActionRank(item.Priority))
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToArray();

        return Results.Ok(new PortfolioCommandCenterResponse(
            PortfolioOverviewCalculator.ContractVersion,
            clock.UtcNow,
            PortfolioHeaderResponse.From(overview),
            projectResponses,
            actionExceptions));
    }

    private static async Task<IReadOnlyDictionary<Guid, ProjectStateSnapshot>> LoadLatestSnapshotsAsync(
        ProjectIntelligenceDbContext dbContext,
        Guid tenantId,
        Guid[] projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Length == 0)
        {
            return new Dictionary<Guid, ProjectStateSnapshot>();
        }

        var ids = projectIds.Distinct().ToArray();
        var latestSnapshotIds = dbContext.ProjectStateSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId && ids.Contains(snapshot.ProjectId))
            .GroupBy(snapshot => snapshot.ProjectId)
            .Select(group => group
                .OrderByDescending(snapshot => snapshot.CalculatedAt)
                .ThenByDescending(snapshot => snapshot.Id)
                .Select(snapshot => snapshot.Id)
                .First());
        var snapshots = await dbContext.ProjectStateSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.AttentionItems)
            .Where(snapshot => latestSnapshotIds.Contains(snapshot.Id))
            .ToArrayAsync(cancellationToken);
        return snapshots.ToDictionary(snapshot => snapshot.ProjectId);
    }

    private static PortfolioProjectResponse BuildProjectResponse(
        ProjectControlProfile project,
        ProjectStateSnapshot? snapshot,
        DateTimeOffset? latestApprovedChange,
        IReadOnlyCollection<string> projectManagers,
        bool canReadFinance,
        FinancialStateRecord? financialState,
        bool canReadCommercial,
        CommercialStateRecord? commercialState,
        bool actionsVisible,
        PortfolioActionRecord[] actions,
        DateOnly localToday)
    {
        var isOutdated = snapshot is not null &&
            (snapshot.ProjectConfigurationRevision != project.Revision ||
                (latestApprovedChange.HasValue &&
                    (snapshot.SourceMaxChangedAt is null || latestApprovedChange > snapshot.SourceMaxChangedAt)));
        var topReasons = snapshot?.AttentionItems
            .OrderByDescending(item => AttentionRank(item.Priority))
            .ThenByDescending(item => item.AgeDays)
            .Select(item => item.Description)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray() ?? [];

        var operational = new PortfolioOperationalStateResponse(
            snapshot is not null,
            isOutdated,
            snapshot is null && latestApprovedChange.HasValue,
            snapshot?.Id,
            snapshot?.CalculationVersion,
            snapshot?.AsOfDate,
            snapshot?.CalculatedAt,
            snapshot?.OperationalStatus ?? ProjectOperationalStatus.NoData,
            snapshot?.CoverageStatus ?? DataCoverageStatus.NoData,
            snapshot?.FreshnessStatus ?? DataFreshnessStatus.NoData,
            snapshot?.ConfidenceStatus ?? DataConfidenceStatus.NoData,
            snapshot?.CoveragePercent,
            snapshot?.ApprovedReportDays,
            snapshot is null ? 0 : snapshot.IssueCount + snapshot.StoppageCount,
            snapshot?.HighImpactCount ?? 0,
            snapshot?.CriticalImpactCount ?? 0,
            topReasons);
        var actionSummary = actionsVisible
            ? new PortfolioActionSummaryResponse(
                true,
                actions.Length,
                actions.Count(action => action.DueDate < localToday),
                actions.Count(action => action.Priority == ActionPriority.Critical),
                actions.Length == 0 ? null : actions.Min(action => action.DueDate))
            : new PortfolioActionSummaryResponse(false, null, null, null, null);

        return new PortfolioProjectResponse(
            project.Id,
            project.Code,
            project.Name,
            project.TimeZone,
            project.BaseCurrencyCode,
            project.Status,
            project.ContractModel,
            project.PlanningMode,
            projectManagers,
            operational,
            canReadFinance,
            canReadFinance && financialState is not null
                ? CommandCenterFinancialStateResponse.From(financialState)
                : null,
            canReadCommercial,
            canReadCommercial && commercialState is not null
                ? CommandCenterCommercialStateResponse.From(commercialState)
                : null,
            actionSummary,
            ProjectIntelligenceEndpoints.BuildCapabilities(project, snapshot, financialState, commercialState));
    }

    private static bool Includes(ProjectPermissionScope scope, Guid projectId) =>
        scope.AllProjects || scope.ProjectIds.Contains(projectId);

    private static DateTimeOffset? LatestSnapshotAt(PortfolioProjectResponse project)
    {
        var candidates = new DateTimeOffset?[]
        {
            project.Operational.SnapshotId.HasValue ? project.Operational.CalculatedAt : null,
            project.Financial?.SnapshotId.HasValue == true ? project.Financial.CalculatedAt : null,
            project.Commercial?.SnapshotId.HasValue == true ? project.Commercial.CalculatedAt : null
        };
        return candidates.Max();
    }

    private static int OperationalRank(ProjectOperationalStatus status) => (int)status;

    private static int ActionRank(ActionPriority priority) => (int)priority;

    private static int AttentionRank(ProjectAttentionPriority priority) => (int)priority;
}
