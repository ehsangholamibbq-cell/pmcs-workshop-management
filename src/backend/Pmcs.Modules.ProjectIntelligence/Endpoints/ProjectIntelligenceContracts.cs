using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.ProjectIntelligence.Persistence;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.ActionControl.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.ProjectIntelligence.Endpoints;

public sealed record RecalculateProjectStateRequest(DateOnly? AsOfDate);

public sealed record ProjectStateSnapshotResponse(
    Guid SnapshotId,
    string CalculationVersion,
    long ProjectConfigurationRevision,
    DateOnly AsOfDate,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    DateTimeOffset CalculatedAt,
    ProjectAssessmentScope AssessmentScope,
    bool IsPartial,
    ProjectOperationalStatus OperationalStatus,
    DataCoverageStatus CoverageStatus,
    DataFreshnessStatus FreshnessStatus,
    DataConfidenceStatus ConfidenceStatus,
    ProjectCoverageBasis CoverageBasis,
    decimal CoveragePercent,
    int ExpectedReportDays,
    int ApprovedReportDays,
    DateOnly? LastApprovedReportDate,
    int ApprovedFactCount,
    int ProgressFactCount,
    int LaborFactCount,
    int EquipmentFactCount,
    int MaterialFactCount,
    int IssueCount,
    int StoppageCount,
    int HighImpactCount,
    int CriticalImpactCount,
    int? OldestAttentionAgeDays,
    DateTimeOffset? SourceMaxChangedAt,
    IReadOnlyCollection<ProjectAttentionResponse> AttentionItems)
{
    internal static ProjectStateSnapshotResponse From(
        ProjectStateSnapshot snapshot,
        IReadOnlyDictionary<Guid, AttentionDispositionRecord>? dispositions = null) => new(
        snapshot.Id,
        snapshot.CalculationVersion,
        snapshot.ProjectConfigurationRevision,
        snapshot.AsOfDate,
        snapshot.WindowStart,
        snapshot.WindowEnd,
        snapshot.CalculatedAt,
        snapshot.AssessmentScope,
        snapshot.IsPartial,
        snapshot.OperationalStatus,
        snapshot.CoverageStatus,
        snapshot.FreshnessStatus,
        snapshot.ConfidenceStatus,
        snapshot.CoverageBasis,
        snapshot.CoveragePercent,
        snapshot.ExpectedReportDays,
        snapshot.ApprovedReportDays,
        snapshot.LastApprovedReportDate,
        snapshot.ApprovedFactCount,
        snapshot.ProgressFactCount,
        snapshot.LaborFactCount,
        snapshot.EquipmentFactCount,
        snapshot.MaterialFactCount,
        snapshot.IssueCount,
        snapshot.StoppageCount,
        snapshot.HighImpactCount,
        snapshot.CriticalImpactCount,
        snapshot.OldestAttentionAgeDays,
        snapshot.SourceMaxChangedAt,
        snapshot.AttentionItems.Select(item => ProjectAttentionResponse.From(
            item,
            dispositions is not null && dispositions.TryGetValue(item.SourceFactId, out var disposition)
                ? disposition
                : null)).ToArray());
}

public sealed record ProjectAttentionResponse(
    Guid SourceReportId,
    Guid SourceFactId,
    DateOnly ReportDate,
    ProjectAttentionKind Kind,
    string Description,
    string? Category,
    string? LocationName,
    ProjectObservedImpact? ObservedImpact,
    ProjectAttentionPriority Priority,
    int AgeDays,
    ProjectAttentionAgeBand AgeBand,
    ProjectAttentionStatus Status,
    string? ReferenceCode,
    AttentionDispositionState Disposition,
    Guid? ActionId,
    string? DispositionReason,
    DateTimeOffset? DispositionAt)
{
    internal static ProjectAttentionResponse From(
        ProjectStateAttentionItem item,
        AttentionDispositionRecord? disposition) => new(
        item.SourceReportId,
        item.SourceFactId,
        item.ReportDate,
        item.Kind,
        item.Description,
        item.Category,
        item.LocationName,
        item.ObservedImpact,
        item.Priority,
        item.AgeDays,
        item.AgeBand,
        item.Status,
        item.ReferenceCode,
        disposition?.Kind switch
        {
            AttentionDispositionRecordKind.ConvertedToAction => AttentionDispositionState.ConvertedToAction,
            AttentionDispositionRecordKind.Dismissed => AttentionDispositionState.Dismissed,
            _ => AttentionDispositionState.NeedsTriage
        },
        disposition?.ActionId,
        disposition?.Reason,
        disposition?.DecidedAt);
}

public enum AttentionDispositionState
{
    NeedsTriage = 1,
    ConvertedToAction = 2,
    Dismissed = 3
}

public sealed record ProjectStateTrendPointResponse(
    Guid SnapshotId,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    ProjectOperationalStatus OperationalStatus,
    decimal CoveragePercent,
    DataFreshnessStatus FreshnessStatus,
    int AttentionCount);

public sealed record ProjectCapabilityResponse(
    string Key,
    string Label,
    ProjectFeatureState ConfigurationState,
    CapabilityMetricState MetricState,
    bool IncludedInAssessment);

public enum CapabilityMetricState
{
    NotApplicable = 1,
    NoData = 2,
    Available = 3
}

public sealed record CommandCenterResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string TimeZone,
    bool HasSnapshot,
    bool IsOutdated,
    bool CanRecalculate,
    bool CanTriage,
    bool CanReadFinance,
    bool CanReadCommercial,
    ProjectStateSnapshotResponse? Snapshot,
    CommandCenterFinancialStateResponse? FinancialState,
    CommandCenterCommercialStateResponse? CommercialState,
    IReadOnlyCollection<ProjectCapabilityResponse> Capabilities,
    IReadOnlyCollection<ProjectStateTrendPointResponse> Trend);

public sealed record CommandCenterFinancialStateResponse(
    Guid? SnapshotId,
    string CalculationVersion,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    string CurrencyCode,
    FinancialStateStatus Status,
    FinancialDataQualityStatus DataQualityStatus,
    BudgetComparisonState BudgetComparisonState,
    int PostedRecordCount,
    decimal TotalReceipts,
    decimal DirectPayments,
    decimal PettyCashFunding,
    decimal PettyCashExpenses,
    decimal ExternalNetCash,
    decimal RecognizedSpend,
    decimal PettyCashBalance,
    decimal? ApprovedBudgetAmount,
    decimal? BudgetRemainingAmount,
    decimal? BudgetConsumedPercent,
    DateTimeOffset? SourceMaxChangedAt)
{
    internal static CommandCenterFinancialStateResponse From(FinancialStateRecord state) => new(
        state.SnapshotId,
        state.CalculationVersion,
        state.AsOfDate,
        state.CalculatedAt,
        state.CurrencyCode,
        state.Status,
        state.DataQualityStatus,
        state.BudgetComparisonState,
        state.PostedRecordCount,
        state.TotalReceipts,
        state.DirectPayments,
        state.PettyCashFunding,
        state.PettyCashExpenses,
        state.ExternalNetCash,
        state.RecognizedSpend,
        state.PettyCashBalance,
        state.ApprovedBudgetAmount,
        state.BudgetRemainingAmount,
        state.BudgetConsumedPercent,
        state.SourceMaxChangedAt);
}

public sealed record CommandCenterCommercialStateResponse(
    Guid? SnapshotId,
    string CalculationVersion,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    string CurrencyCode,
    CommercialMetricState ContractState,
    CommercialDataQualityStatus ContractDataQualityStatus,
    CommercialMetricState ProcurementState,
    CommercialDataQualityStatus ProcurementDataQualityStatus,
    int ActivePartyCount,
    int RegisteredContractCount,
    int ActiveContractCount,
    int ContractsWithoutCeilingCount,
    int PendingContractApprovalCount,
    int ExpiredActiveContractCount,
    int ApprovedAmendmentCount,
    decimal ApprovedAmendmentDelta,
    decimal? ApprovedContractCeilingAmount,
    int PurchaseRequestCount,
    int PendingProcurementApprovalCount,
    int ApprovedRequestsAwaitingOrderCount,
    int OpenCommitmentCount,
    int OverdueCommitmentCount,
    decimal TotalCommittedAmount,
    decimal OpenCommitmentAmount,
    DateTimeOffset? SourceMaxChangedAt)
{
    internal static CommandCenterCommercialStateResponse From(CommercialStateRecord state) => new(
        state.SnapshotId,
        state.CalculationVersion,
        state.AsOfDate,
        state.CalculatedAt,
        state.CurrencyCode,
        state.ContractState,
        state.ContractDataQualityStatus,
        state.ProcurementState,
        state.ProcurementDataQualityStatus,
        state.ActivePartyCount,
        state.RegisteredContractCount,
        state.ActiveContractCount,
        state.ContractsWithoutCeilingCount,
        state.PendingContractApprovalCount,
        state.ExpiredActiveContractCount,
        state.ApprovedAmendmentCount,
        state.ApprovedAmendmentDelta,
        state.ApprovedContractCeilingAmount,
        state.PurchaseRequestCount,
        state.PendingProcurementApprovalCount,
        state.ApprovedRequestsAwaitingOrderCount,
        state.OpenCommitmentCount,
        state.OverdueCommitmentCount,
        state.TotalCommittedAmount,
        state.OpenCommitmentAmount,
        state.SourceMaxChangedAt);
}

public sealed record PortfolioCommandCenterResponse(
    string ContractVersion,
    DateTimeOffset GeneratedAt,
    PortfolioHeaderResponse Header,
    IReadOnlyCollection<PortfolioProjectResponse> Projects,
    IReadOnlyCollection<PortfolioActionExceptionResponse> ActionExceptions);

public sealed record PortfolioHeaderResponse(
    int ProjectCount,
    int ActiveProjectCount,
    int OnHoldProjectCount,
    int ClosingProjectCount,
    int StableProjectCount,
    int WatchProjectCount,
    int AtRiskProjectCount,
    int CriticalProjectCount,
    int InsufficientDataProjectCount,
    int NoDataProjectCount,
    int StaleProjectCount,
    int OutdatedProjectCount,
    int ActionVisibleProjectCount,
    int OpenActionCount,
    int OverdueActionCount,
    int PendingCommercialApprovalCount,
    DateTimeOffset? LatestSnapshotAt,
    IReadOnlyCollection<PortfolioCurrencyExposureResponse> CurrencyExposures)
{
    internal static PortfolioHeaderResponse From(PortfolioOverviewCalculation calculation) => new(
        calculation.ProjectCount,
        calculation.ActiveProjectCount,
        calculation.OnHoldProjectCount,
        calculation.ClosingProjectCount,
        calculation.StableProjectCount,
        calculation.WatchProjectCount,
        calculation.AtRiskProjectCount,
        calculation.CriticalProjectCount,
        calculation.InsufficientDataProjectCount,
        calculation.NoDataProjectCount,
        calculation.StaleProjectCount,
        calculation.OutdatedProjectCount,
        calculation.ActionVisibleProjectCount,
        calculation.OpenActionCount,
        calculation.OverdueActionCount,
        calculation.PendingCommercialApprovalCount,
        calculation.LatestSnapshotAt,
        calculation.CurrencyExposures.Select(PortfolioCurrencyExposureResponse.From).ToArray());
}

public sealed record PortfolioCurrencyExposureResponse(
    string CurrencyCode,
    int FinancialProjectCount,
    int CommercialProjectCount,
    decimal RecognizedSpend,
    decimal ExternalNetCash,
    decimal TotalCommittedAmount,
    decimal OpenCommitmentAmount)
{
    internal static PortfolioCurrencyExposureResponse From(PortfolioCurrencyExposure exposure) => new(
        exposure.CurrencyCode,
        exposure.FinancialProjectCount,
        exposure.CommercialProjectCount,
        exposure.RecognizedSpend,
        exposure.ExternalNetCash,
        exposure.TotalCommittedAmount,
        exposure.OpenCommitmentAmount);
}

public sealed record PortfolioProjectResponse(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string TimeZone,
    string BaseCurrencyCode,
    ProjectStatus LifecycleStatus,
    ContractModel ContractModel,
    PlanningMode PlanningMode,
    IReadOnlyCollection<string> ProjectManagers,
    PortfolioOperationalStateResponse Operational,
    bool CanReadFinance,
    CommandCenterFinancialStateResponse? Financial,
    bool CanReadCommercial,
    CommandCenterCommercialStateResponse? Commercial,
    PortfolioActionSummaryResponse Actions,
    IReadOnlyCollection<ProjectCapabilityResponse> Capabilities);

public sealed record PortfolioOperationalStateResponse(
    bool HasSnapshot,
    bool IsOutdated,
    bool NeedsCalculation,
    Guid? SnapshotId,
    string? CalculationVersion,
    DateOnly? AsOfDate,
    DateTimeOffset? CalculatedAt,
    ProjectOperationalStatus Status,
    DataCoverageStatus CoverageStatus,
    DataFreshnessStatus FreshnessStatus,
    DataConfidenceStatus ConfidenceStatus,
    decimal? CoveragePercent,
    int? ApprovedReportDays,
    int AttentionCount,
    int HighImpactCount,
    int CriticalImpactCount,
    IReadOnlyCollection<string> TopReasons);

public sealed record PortfolioActionSummaryResponse(
    bool IsVisible,
    int? OpenCount,
    int? OverdueCount,
    int? CriticalOpenCount,
    DateOnly? NextDueDate);

public sealed record PortfolioActionExceptionResponse(
    Guid ActionId,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string Title,
    Guid AssigneeUserId,
    string AssigneeDisplayName,
    DateOnly DueDate,
    bool IsOverdue,
    ActionPriority Priority,
    ManagementActionStatus Status,
    long Revision);
