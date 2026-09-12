using System.Text.Json;
using Pmcs.BuildingBlocks.Application;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Intelligence.Domain;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Intelligence.Services;

public sealed record AdvisoryContext(
    Guid TenantId,
    Guid ProjectId,
    Guid RequestedBy,
    Guid SnapshotId,
    string ContextJson,
    string ContextHash,
    IReadOnlySet<string> AllowedEvidenceReferences,
    bool IncludesFinancialData,
    bool IncludesCommercialData,
    bool IncludesActionData);

public sealed class AdvisoryContextException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

internal sealed class PermissionAwareContextAssembler(
    IProjectPermissionService permissionService,
    IProjectDirectory projectDirectory,
    IProjectStateContextSource projectStateSource,
    IFinancialStateSource financialStateSource,
    ICommercialStateSource commercialStateSource,
    IPortfolioActionSource actionSource)
{
    public async Task<AdvisoryContext> AssembleAsync(
        Guid tenantId,
        Guid projectId,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (!await permissionService.HasProjectPermissionAsync(
                tenantId, requestedBy, projectId, "insights.generate", cancellationToken))
        {
            throw new AdvisoryContextException("ai.permission.revoked");
        }

        var project = await projectDirectory.FindProfileAsync(tenantId, projectId, cancellationToken)
            ?? throw new AdvisoryContextException("project.not_found");
        var snapshot = await projectStateSource.GetLatestAsync(tenantId, projectId, cancellationToken)
            ?? throw new AdvisoryContextException("ai.context.no_snapshot");
        if (snapshot.ProjectConfigurationRevision != project.Revision)
        {
            throw new AdvisoryContextException("ai.context.stale_snapshot");
        }

        var includeFinance = await permissionService.HasProjectPermissionAsync(
            tenantId, requestedBy, projectId, "financial-state.read", cancellationToken);
        var includeCommercial = await permissionService.HasProjectPermissionAsync(
            tenantId, requestedBy, projectId, "commercial-state.read", cancellationToken);
        var includeActions = await permissionService.HasProjectPermissionAsync(
            tenantId, requestedBy, projectId, "actions.read", cancellationToken);

        var financialState = includeFinance
            ? await financialStateSource.GetCurrentAsync(tenantId, projectId, cancellationToken)
            : null;
        var commercialState = includeCommercial
            ? await commercialStateSource.GetCurrentAsync(tenantId, projectId, cancellationToken)
            : null;
        var actions = includeActions
            ? await actionSource.ListOpenAsync(tenantId, [projectId], cancellationToken)
            : [];

        var citations = new HashSet<string>(StringComparer.Ordinal)
        {
            $"project-state:{snapshot.SnapshotId:D}"
        };
        foreach (var attention in snapshot.AttentionItems.Take(20))
        {
            citations.Add($"daily-fact:{attention.SourceFactId:D}");
        }

        var financialCitation = financialState is null
            ? null
            : financialState.SnapshotId.HasValue
                ? $"financial-state:{financialState.SnapshotId.Value:D}"
                : $"financial-state:calculated:{financialState.AsOfDate:yyyy-MM-dd}";
        if (financialCitation is not null)
        {
            citations.Add(financialCitation);
        }

        var commercialCitation = commercialState is null
            ? null
            : commercialState.SnapshotId.HasValue
                ? $"commercial-state:{commercialState.SnapshotId.Value:D}"
                : $"commercial-state:calculated:{commercialState.AsOfDate:yyyy-MM-dd}";
        if (commercialCitation is not null)
        {
            citations.Add(commercialCitation);
        }

        foreach (var action in actions.Take(50))
        {
            citations.Add($"management-action:{action.Id:D}");
        }

        var context = new
        {
            contractVersion = "pmcs-advisory-context-v1",
            scope = new { project.Id, project.Code, project.Name, project.TimeZone, project.BaseCurrencyCode },
            policy = new
            {
                outputLanguage = "fa-IR",
                advisoryOnly = true,
                officialStateMustNotBeChanged = true,
                optionalCapabilitiesAreNotNegativeSignals = true
            },
            capabilityConfiguration = new
            {
                contract = project.Contract.ToString(),
                planning = project.Planning.ToString(),
                planningMode = project.PlanningMode.ToString(),
                budget = project.Budget.ToString(),
                quality = project.Quality.ToString(),
                hse = project.Hse.ToString(),
                finance = project.Finance.ToString(),
                procurement = project.Procurement.ToString(),
                calendar = project.Calendar.State.ToString()
            },
            projectState = new
            {
                citation = $"project-state:{snapshot.SnapshotId:D}",
                snapshot.SnapshotId,
                snapshot.CalculationVersion,
                snapshot.AsOfDate,
                snapshot.CalculatedAt,
                snapshot.AssessmentScope,
                snapshot.IsPartial,
                snapshot.OperationalStatus,
                snapshot.CoverageStatus,
                snapshot.FreshnessStatus,
                snapshot.ConfidenceStatus,
                snapshot.CoveragePercent,
                snapshot.ExpectedReportDays,
                snapshot.ApprovedReportDays,
                snapshot.LastApprovedReportDate,
                factCounts = new
                {
                    approved = snapshot.ApprovedFactCount,
                    progress = snapshot.ProgressFactCount,
                    labor = snapshot.LaborFactCount,
                    equipment = snapshot.EquipmentFactCount,
                    material = snapshot.MaterialFactCount,
                    issues = snapshot.IssueCount,
                    stoppages = snapshot.StoppageCount
                },
                impactCounts = new { high = snapshot.HighImpactCount, critical = snapshot.CriticalImpactCount },
                snapshot.OldestAttentionAgeDays,
                attentionItems = snapshot.AttentionItems.Take(20).Select(item => new
                {
                    citation = $"daily-fact:{item.SourceFactId:D}",
                    item.ReportDate,
                    item.Kind,
                    item.Description,
                    item.Category,
                    item.LocationName,
                    item.ObservedImpact,
                    item.Priority,
                    item.AgeDays,
                    item.Status,
                    item.ReferenceCode
                }).ToArray()
            },
            financialState = financialState is null ? null : new
            {
                citation = financialCitation,
                financialState.AsOfDate,
                financialState.CalculatedAt,
                financialState.CurrencyCode,
                status = financialState.Status.ToString(),
                dataQualityStatus = financialState.DataQualityStatus.ToString(),
                budgetComparisonState = financialState.BudgetComparisonState.ToString(),
                financialState.PostedRecordCount,
                financialState.TotalReceipts,
                financialState.DirectPayments,
                financialState.PettyCashFunding,
                financialState.PettyCashExpenses,
                financialState.ExternalNetCash,
                financialState.RecognizedSpend,
                financialState.PettyCashBalance,
                financialState.ApprovedBudgetAmount,
                financialState.BudgetRemainingAmount,
                financialState.BudgetConsumedPercent
            },
            commercialState = commercialState is null ? null : new
            {
                citation = commercialCitation,
                commercialState.AsOfDate,
                commercialState.CalculatedAt,
                commercialState.CurrencyCode,
                contractState = commercialState.ContractState.ToString(),
                contractDataQualityStatus = commercialState.ContractDataQualityStatus.ToString(),
                procurementState = commercialState.ProcurementState.ToString(),
                procurementDataQualityStatus = commercialState.ProcurementDataQualityStatus.ToString(),
                commercialState.ActivePartyCount,
                commercialState.RegisteredContractCount,
                commercialState.ActiveContractCount,
                commercialState.ContractsWithoutCeilingCount,
                commercialState.PendingContractApprovalCount,
                commercialState.ExpiredActiveContractCount,
                commercialState.ApprovedAmendmentCount,
                commercialState.ApprovedAmendmentDelta,
                commercialState.ApprovedContractCeilingAmount,
                commercialState.PurchaseRequestCount,
                commercialState.PendingProcurementApprovalCount,
                commercialState.ApprovedRequestsAwaitingOrderCount,
                commercialState.OpenCommitmentCount,
                commercialState.OverdueCommitmentCount,
                commercialState.TotalCommittedAmount,
                commercialState.OpenCommitmentAmount
            },
            openActions = actions.Take(50).Select(item => new
            {
                citation = $"management-action:{item.Id:D}",
                item.Title,
                item.DueDate,
                priority = item.Priority.ToString(),
                status = item.Status.ToString()
            }).ToArray(),
            accessScope = new
            {
                financialStateIncluded = includeFinance,
                commercialStateIncluded = includeCommercial,
                openActionsIncluded = includeActions
            },
            knownDataGaps = BuildKnownDataGaps(project, snapshot, financialState, commercialState)
        };

        var contextJson = JsonSerializer.Serialize(context, AdvisoryJson.Options);
        return new AdvisoryContext(
            tenantId,
            projectId,
            requestedBy,
            snapshot.SnapshotId,
            contextJson,
            RequestHash.Create(contextJson),
            citations,
            includeFinance && financialState is not null,
            includeCommercial && commercialState is not null,
            includeActions);
    }

    private static List<string> BuildKnownDataGaps(
        ProjectControlProfile project,
        ProjectStateContextRecord snapshot,
        FinancialStateRecord? financialState,
        CommercialStateRecord? commercialState)
    {
        var gaps = new List<string>();
        if (project.Planning is not ProjectFeatureState.Active)
        {
            gaps.Add("planning_or_wbs_not_available_and_must_not_be_treated_as_negative");
        }
        if (project.Budget is not ProjectFeatureState.Active || financialState?.ApprovedBudgetAmount is null)
        {
            gaps.Add("approved_budget_baseline_not_available_and_budget_variance_cannot_be_inferred");
        }
        if (project.Hse is not ProjectFeatureState.Active)
        {
            gaps.Add("hse_capability_not_active_and_safety_status_cannot_be_inferred");
        }
        if (snapshot.IsPartial || !string.Equals(snapshot.ConfidenceStatus, "Adequate", StringComparison.Ordinal))
        {
            gaps.Add("project_state_is_partial_or_has_limited_confidence");
        }
        if (financialState is null)
        {
            gaps.Add("financial_state_not_in_authorized_context");
        }
        if (commercialState is null)
        {
            gaps.Add("commercial_state_not_in_authorized_context");
        }
        return gaps;
    }
}
