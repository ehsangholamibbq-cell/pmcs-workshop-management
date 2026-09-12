using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Domain;

public static class FinancialStateCalculator
{
    public const string CalculationVersion = "financial-state-v1";

    public static FinancialStateCalculation Calculate(
        ProjectControlProfile project,
        IReadOnlyCollection<PostedFinancialEntry> source,
        ApprovedBudget? budget,
        DateOnly asOfDate,
        DateTimeOffset calculatedAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);

        var entries = source.Where(item => item.TransactionDate <= asOfDate).ToArray();
        if (entries.Any(item => !string.Equals(item.CurrencyCode, project.BaseCurrencyCode, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Financial state cannot aggregate currencies outside the project base currency.");
        }

        var receipts = Sum(entries, FinancialRecordType.Receipt);
        var payments = Sum(entries, FinancialRecordType.Payment);
        var pettyCashFunding = Sum(entries, FinancialRecordType.PettyCashFunding);
        var pettyCashExpense = Sum(entries, FinancialRecordType.PettyCashExpense);
        var recognizedSpend = payments + pettyCashExpense;
        var pettyCashBalance = pettyCashFunding - pettyCashExpense;
        var status = project.Finance is ProjectFeatureState.NotConfigured or ProjectFeatureState.NotEnabled or ProjectFeatureState.Suspended
            ? FinancialStateStatus.NotConfigured
            : entries.Length == 0
                ? FinancialStateStatus.NoData
                : FinancialStateStatus.Available;
        var budgetState = budget is not null
            ? BudgetComparisonState.Available
            : project.Budget switch
            {
                ProjectFeatureState.NotConfigured or ProjectFeatureState.NotEnabled => BudgetComparisonState.NotConfigured,
                ProjectFeatureState.SetupRequired => BudgetComparisonState.SetupRequired,
                ProjectFeatureState.Active => BudgetComparisonState.NoData,
                ProjectFeatureState.Suspended => BudgetComparisonState.Suspended,
                _ => throw new ArgumentOutOfRangeException(nameof(project), project.Budget, "Unsupported budget state.")
            };

        return new FinancialStateCalculation(
            project.TenantId,
            project.Id,
            CalculationVersion,
            asOfDate,
            calculatedAt,
            project.BaseCurrencyCode,
            status,
            entries.Length == 0
                ? FinancialDataQualityStatus.NoData
                : pettyCashBalance < 0
                    ? FinancialDataQualityStatus.NeedsAttention
                    : FinancialDataQualityStatus.Adequate,
            budgetState,
            entries.Length,
            receipts,
            payments,
            pettyCashFunding,
            pettyCashExpense,
            receipts - payments - pettyCashFunding,
            recognizedSpend,
            pettyCashBalance,
            budget?.Amount,
            budget is null ? null : budget.Amount - recognizedSpend,
            budget is null ? null : Math.Round(recognizedSpend * 100m / budget.Amount, 1, MidpointRounding.AwayFromZero),
            MaxTimestamp(entries.Select(item => (DateTimeOffset?)item.PostedAt).Append(budget?.ApprovedAt)));
    }

    private static decimal Sum(IEnumerable<PostedFinancialEntry> source, FinancialRecordType type) =>
        source.Where(item => item.Type == type).Sum(item => item.Amount);

    private static DateTimeOffset? MaxTimestamp(IEnumerable<DateTimeOffset?> values)
    {
        var available = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return available.Length == 0 ? null : available.Max();
    }
}

public sealed record PostedFinancialEntry(
    Guid Id,
    FinancialRecordType Type,
    DateOnly TransactionDate,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset PostedAt);

public sealed record ApprovedBudget(Guid Id, decimal Amount, string CurrencyCode, DateTimeOffset ApprovedAt);

public sealed record FinancialStateCalculation(
    Guid TenantId,
    Guid ProjectId,
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

public enum FinancialStateStatus
{
    NotConfigured = 1,
    NoData = 2,
    Available = 3
}

public enum FinancialDataQualityStatus
{
    NoData = 1,
    Adequate = 2,
    NeedsAttention = 3
}

public enum BudgetComparisonState
{
    NotConfigured = 1,
    SetupRequired = 2,
    NoData = 3,
    Available = 4,
    Suspended = 5
}
