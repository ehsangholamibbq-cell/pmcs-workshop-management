using Pmcs.Modules.Finance.Domain;

namespace Pmcs.Modules.Finance.Persistence;

internal sealed class FinancialStateSnapshot
{
    private FinancialStateSnapshot()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string CalculationVersion { get; private set; } = string.Empty;

    public DateOnly AsOfDate { get; private set; }

    public DateTimeOffset CalculatedAt { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public FinancialStateStatus Status { get; private set; }

    public FinancialDataQualityStatus DataQualityStatus { get; private set; }

    public BudgetComparisonState BudgetComparisonState { get; private set; }

    public int PostedRecordCount { get; private set; }

    public decimal TotalReceipts { get; private set; }

    public decimal DirectPayments { get; private set; }

    public decimal PettyCashFunding { get; private set; }

    public decimal PettyCashExpenses { get; private set; }

    public decimal ExternalNetCash { get; private set; }

    public decimal RecognizedSpend { get; private set; }

    public decimal PettyCashBalance { get; private set; }

    public decimal? ApprovedBudgetAmount { get; private set; }

    public decimal? BudgetRemainingAmount { get; private set; }

    public decimal? BudgetConsumedPercent { get; private set; }

    public DateTimeOffset? SourceMaxChangedAt { get; private set; }

    public static FinancialStateSnapshot Create(Guid id, FinancialStateCalculation calculation)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Snapshot id is required.", nameof(id));
        }

        return new FinancialStateSnapshot
        {
            Id = id,
            TenantId = calculation.TenantId,
            ProjectId = calculation.ProjectId,
            CalculationVersion = calculation.CalculationVersion,
            AsOfDate = calculation.AsOfDate,
            CalculatedAt = calculation.CalculatedAt,
            CurrencyCode = calculation.CurrencyCode,
            Status = calculation.Status,
            DataQualityStatus = calculation.DataQualityStatus,
            BudgetComparisonState = calculation.BudgetComparisonState,
            PostedRecordCount = calculation.PostedRecordCount,
            TotalReceipts = calculation.TotalReceipts,
            DirectPayments = calculation.DirectPayments,
            PettyCashFunding = calculation.PettyCashFunding,
            PettyCashExpenses = calculation.PettyCashExpenses,
            ExternalNetCash = calculation.ExternalNetCash,
            RecognizedSpend = calculation.RecognizedSpend,
            PettyCashBalance = calculation.PettyCashBalance,
            ApprovedBudgetAmount = calculation.ApprovedBudgetAmount,
            BudgetRemainingAmount = calculation.BudgetRemainingAmount,
            BudgetConsumedPercent = calculation.BudgetConsumedPercent,
            SourceMaxChangedAt = calculation.SourceMaxChangedAt
        };
    }
}
