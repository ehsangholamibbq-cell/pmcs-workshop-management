using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ProjectFinancialPositionReportSemanticSnapshot(
    string SchemaVersion,
    string SemanticContractId,
    string DefinitionCode,
    string DefinitionVersion,
    string PolicyVersion,
    ReportDataStatus DataStatus,
    IReadOnlyCollection<ProjectFinancialPositionReasonCode> ReasonCodes,
    ProjectFinancialPositionReportParameters Parameters,
    ProjectFinancialPositionReportProjectIdentity Project,
    ProjectFinancialPositionReportCutoffIdentity Cutoff,
    ReportClassification Classification,
    ProjectFinancialPositionReportConfiguration? Configuration,
    ProjectFinancialPositionSectionStatus CashStatus,
    ProjectFinancialPositionReportCashSummary Cash,
    ProjectFinancialPositionSectionStatus ObligationStatus,
    ProjectFinancialPositionSectionStatus BudgetStatus,
    ProjectFinancialPositionSectionStatus BudgetComparisonStatus,
    ProjectFinancialPositionReportBudgetIdentity? Budget,
    ProjectFinancialPositionReportBudgetComparison? BudgetComparison,
    ProjectFinancialPositionReportObligationSummary? PayableSummary,
    ProjectFinancialPositionReportObligationSummary? ReceivableSummary,
    IReadOnlyCollection<ProjectFinancialPositionReportAgingSummary> Aging,
    IReadOnlyCollection<ProjectFinancialPositionReportOpenObligation> OpenObligations,
    ProjectFinancialSourceCounts SourceCounts,
    DateTimeOffset? SourceMaxChangedAt,
    string SourceManifestSha256);

internal sealed record ProjectFinancialPositionReportProjectIdentity(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    string CapturedBaseCurrencyCode,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt,
    DateTimeOffset ProfileCapturedAtUtc);

internal sealed record ProjectFinancialPositionReportCutoffIdentity(
    DateTimeOffset SourceCutoffUtc,
    DateOnly CutoffLocalDate);

internal sealed record ProjectFinancialPositionReportConfiguration(
    long ConfigurationVersion,
    long ProjectRevision,
    ProjectFeatureState FinanceState,
    ProjectFeatureState BudgetState,
    string BaseCurrencyCode,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc);

internal sealed record ProjectFinancialPositionReportCashSummary(
    decimal? TotalReceipts,
    decimal? DirectPayments,
    decimal? PettyCashFunding,
    decimal? PettyCashExpenses,
    decimal? ExternalNetCash,
    decimal? RecognizedSpend,
    decimal? PettyCashBalance);

internal sealed record ProjectFinancialPositionReportBudgetIdentity(
    Guid BaselineId,
    long Revision,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt);

internal sealed record ProjectFinancialPositionReportBudgetComparison(
    decimal ApprovedBudgetAmount,
    decimal? BudgetRemainingAmount,
    decimal? BudgetConsumedPercent);

internal sealed record ProjectFinancialPositionReportObligationSummary(
    FinancialObligationType Type,
    int OpenCount,
    decimal OpenAmount,
    int OverdueCount,
    decimal OverdueAmount);

internal sealed record ProjectFinancialPositionReportAgingSummary(
    FinancialObligationType Type,
    ProjectFinancialPositionAgingBucket Bucket,
    int Count,
    decimal Amount);

internal sealed record ProjectFinancialPositionReportOpenObligation(
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
