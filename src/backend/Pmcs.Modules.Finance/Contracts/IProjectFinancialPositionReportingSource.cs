using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Contracts;

public interface IProjectFinancialPositionReportingSource
{
    Task<ProjectFinancialPositionReportingResult> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default);
}

public static class ProjectFinancialPositionReportingContract
{
    public const string Version = "pmcs.finance.project-financial-position-reporting/v1";
    public const string SourceManifestVersion = "pmcs.finance.project-financial-position-manifest/v1";
    public const string PolicyVersion = "pmcs.finance.project-financial-position-policy/v1";
    public const int MaximumFinancialRecords = 100_000;
    public const int MaximumObligations = 20_000;
    public const int MaximumSettlements = 100_000;
    public const int MaximumBudgetBaselines = 10_000;
}

public sealed record ProjectFinancialPositionReportingProjection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    IReadOnlyCollection<ProjectFinancialConfigurationVersion> Configurations,
    IReadOnlyCollection<ProjectFinancialRecordVersion> FinancialRecords,
    IReadOnlyCollection<ProjectFinancialObligationVersion> Obligations,
    IReadOnlyCollection<ProjectFinancialSettlementVersion> Settlements,
    IReadOnlyCollection<ProjectBudgetBaselineVersion> BudgetBaselines,
    ProjectFinancialSourceCompleteness LedgerCompleteness,
    ProjectFinancialSourceCompleteness ObligationCompleteness,
    ProjectFinancialSourceCompleteness SettlementLineageCompleteness,
    ProjectFinancialPositionReportingClassification Classification);

public sealed record ProjectFinancialConfigurationVersion(
    long ConfigurationVersion,
    long ProjectRevision,
    ProjectFeatureState FinanceState,
    ProjectFeatureState BudgetState,
    string BaseCurrencyCode,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    ProjectFinancialPositionReportingClassification Classification);

public sealed record ProjectFinancialRecordVersion(
    Guid RecordId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    FinancialRecordType Type,
    FinancialRecordStatus Status,
    DateOnly TransactionDate,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    ProjectFinancialPositionReportingClassification Classification);

public sealed record ProjectFinancialObligationVersion(
    Guid ObligationId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    FinancialObligationType Type,
    FinancialObligationStatus Status,
    string NumberSnapshot,
    string? CounterpartySnapshot,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    ProjectFinancialPositionReportingClassification Classification);

public sealed record ProjectFinancialSettlementVersion(
    Guid SettlementId,
    Guid TenantId,
    Guid ProjectId,
    Guid ObligationId,
    Guid FinancialRecordId,
    decimal Amount,
    DateTimeOffset SettledAt,
    ProjectFinancialPositionReportingClassification Classification);

public sealed record ProjectBudgetBaselineVersion(
    Guid BaselineId,
    Guid TenantId,
    Guid ProjectId,
    long Revision,
    BudgetBaselineStatus Status,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? SupersededAt,
    ProjectFinancialPositionReportingClassification Classification);

public sealed record ProjectFinancialPositionReportingSelection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectFinancialConfigurationVersion? Configuration,
    IReadOnlyCollection<ProjectFinancialRecordVersion> OfficialFinancialRecords,
    IReadOnlyCollection<ProjectFinancialObligationVersion> OfficialObligations,
    IReadOnlyCollection<ProjectFinancialSettlementVersion> EligibleSettlements,
    ProjectBudgetBaselineVersion? BudgetBaseline,
    ProjectFinancialSourceCompleteness LedgerCompleteness,
    ProjectFinancialSourceCompleteness ObligationCompleteness,
    ProjectFinancialSourceCompleteness SettlementLineageCompleteness,
    ProjectFinancialPositionReportingClassification Classification,
    ProjectFinancialSourceCounts SourceCounts,
    DateTimeOffset? SourceMaxChangedAt,
    ProjectFinancialPositionSourceManifest SourceManifest,
    string SourceManifestSha256);

public sealed record ProjectFinancialPositionReportingResult(
    string ContractVersion,
    string PolicyVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectFinancialPositionReportingClassification Classification,
    ProjectFinancialPositionDataStatus DataStatus,
    IReadOnlyCollection<ProjectFinancialPositionReasonCode> ReasonCodes,
    ProjectFinancialConfigurationVersion? Configuration,
    ProjectFinancialPositionSectionStatus CashStatus,
    ProjectFinancialPositionCashSummary Cash,
    ProjectFinancialPositionSectionStatus ObligationStatus,
    ProjectFinancialPositionSectionStatus BudgetStatus,
    ProjectFinancialPositionSectionStatus BudgetComparisonStatus,
    ProjectFinancialPositionBudgetIdentity? Budget,
    ProjectFinancialPositionBudgetComparison? BudgetComparison,
    ProjectFinancialPositionObligationSummary? PayableSummary,
    ProjectFinancialPositionObligationSummary? ReceivableSummary,
    IReadOnlyCollection<ProjectFinancialPositionAgingSummary> Aging,
    IReadOnlyCollection<ProjectFinancialPositionOpenObligation> OpenObligations,
    ProjectFinancialSourceCounts SourceCounts,
    DateTimeOffset? SourceMaxChangedAt,
    ProjectFinancialPositionSourceManifest SourceManifest,
    string SourceManifestSha256);

public sealed record ProjectFinancialPositionCashSummary(
    decimal? TotalReceipts,
    decimal? DirectPayments,
    decimal? PettyCashFunding,
    decimal? PettyCashExpenses,
    decimal? ExternalNetCash,
    decimal? RecognizedSpend,
    decimal? PettyCashBalance);

public sealed record ProjectFinancialPositionBudgetIdentity(
    Guid BaselineId,
    long Revision,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt);

public sealed record ProjectFinancialPositionBudgetComparison(
    decimal ApprovedBudgetAmount,
    decimal? BudgetRemainingAmount,
    decimal? BudgetConsumedPercent);

public sealed record ProjectFinancialPositionObligationSummary(
    FinancialObligationType Type,
    int OpenCount,
    decimal OpenAmount,
    int OverdueCount,
    decimal OverdueAmount);

public sealed record ProjectFinancialPositionAgingSummary(
    FinancialObligationType Type,
    ProjectFinancialPositionAgingBucket Bucket,
    int Count,
    decimal Amount);

public sealed record ProjectFinancialPositionOpenObligation(
    Guid ObligationId,
    FinancialObligationType Type,
    string NumberSnapshot,
    string? CounterpartySnapshot,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Amount,
    decimal SettledAmountAtCutoff,
    decimal OutstandingAmount,
    ProjectFinancialPositionAgingBucket Bucket);

public sealed record ProjectFinancialSourceCounts(
    int FinancialRecordSourceCount,
    int OfficialFinancialRecordCount,
    int ExcludedFinancialRecordCount,
    int ObligationSourceCount,
    int OfficialObligationCount,
    int OpenObligationCount,
    int ExcludedObligationCount,
    int SettlementSourceCount,
    int EligibleSettlementCount,
    int ExcludedSettlementCount,
    int BudgetBaselineSourceCount,
    int EffectiveBudgetBaselineCount,
    int ExcludedBudgetBaselineCount,
    int IncompleteCollectionCount);

public sealed record ProjectFinancialPositionSourceManifest(
    string ManifestVersion,
    string SourceContractVersion,
    string PolicyVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectFinancialConfigurationManifest? Configuration,
    IReadOnlyCollection<ProjectFinancialRecordManifest> FinancialRecords,
    IReadOnlyCollection<ProjectFinancialObligationManifest> Obligations,
    IReadOnlyCollection<ProjectFinancialSettlementManifest> Settlements,
    IReadOnlyCollection<ProjectBudgetBaselineManifest> BudgetBaselines,
    ProjectFinancialSourceCompleteness LedgerCompleteness,
    ProjectFinancialSourceCompleteness ObligationCompleteness,
    ProjectFinancialSourceCompleteness SettlementLineageCompleteness);

public sealed record ProjectFinancialConfigurationManifest(
    long ConfigurationVersion,
    long ProjectRevision,
    ProjectFeatureState FinanceState,
    ProjectFeatureState BudgetState,
    string BaseCurrencyCode,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc);

public sealed record ProjectFinancialRecordManifest(
    Guid RecordId,
    long Revision,
    FinancialRecordType Type,
    FinancialRecordStatus Status,
    DateOnly TransactionDate,
    DateTimeOffset? PostedAt,
    string DefinitionSha256);

public sealed record ProjectFinancialObligationManifest(
    Guid ObligationId,
    long Revision,
    FinancialObligationType Type,
    FinancialObligationStatus Status,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateTimeOffset? ApprovedAt,
    string DefinitionSha256);

public sealed record ProjectFinancialSettlementManifest(
    Guid SettlementId,
    Guid ObligationId,
    Guid FinancialRecordId,
    DateTimeOffset SettledAt,
    string DefinitionSha256);

public sealed record ProjectBudgetBaselineManifest(
    Guid BaselineId,
    long Revision,
    BudgetBaselineStatus Status,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? SupersededAt,
    string DefinitionSha256);

public enum ProjectFinancialPositionReportingClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

public enum ProjectFinancialSourceCompleteness
{
    Complete = 1,
    Incomplete = 2
}

public enum ProjectFinancialPositionDataStatus
{
    NotConfigured = 1,
    NoData = 2,
    InsufficientData = 3,
    Available = 4
}

public enum ProjectFinancialPositionSectionStatus
{
    NotConfigured = 1,
    SetupRequired = 2,
    Suspended = 3,
    NoData = 4,
    InsufficientData = 5,
    Available = 6
}

public enum ProjectFinancialPositionAgingBucket
{
    NotDue = 1,
    Overdue1To30 = 2,
    Overdue31To60 = 3,
    Overdue61Plus = 4
}

public enum ProjectFinancialPositionReasonCode
{
    FinanceReportingNotConfigured = 1,
    FinanceSetupRequired = 2,
    FinanceSuspended = 3,
    OfficialFinancialRecordsMissing = 4,
    OfficialObligationsMissing = 5,
    FinancialSourceIncomplete = 6,
    ObligationSettlementLineageIncomplete = 7,
    BudgetNotConfigured = 8,
    BudgetSetupRequired = 9,
    BudgetSuspended = 10,
    OfficialBudgetBaselineMissing = 11,
    OfficialCashDataMissingForBudgetComparison = 12,
    NegativePettyCashBalance = 13
}
