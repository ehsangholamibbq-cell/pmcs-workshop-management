using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Contracts;

public interface IFinancialStateSource
{
    Task<FinancialStateRecord?> GetCurrentAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, FinancialStateRecord>> GetPortfolioAsync(
        Guid tenantId,
        IReadOnlyCollection<ProjectControlProfile> projects,
        CancellationToken cancellationToken = default);
}

public sealed record FinancialStateRecord(
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
    DateTimeOffset? SourceMaxChangedAt);
