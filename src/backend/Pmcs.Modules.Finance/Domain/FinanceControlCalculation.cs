namespace Pmcs.Modules.Finance.Domain;

public static class FinanceControlCalculator
{
    public const string CalculationVersion = "finance-control-v1";

    public static FinanceControlCalculation Calculate(
        DateOnly asOfDate,
        decimal recognizedSpend,
        IReadOnlyCollection<FinancialObligationEntry> obligations,
        IReadOnlyCollection<PettyCashControlEntry> pettyCashRequests,
        ApprovedManagementFeePolicy? managementFeePolicy)
    {
        ArgumentNullException.ThrowIfNull(obligations);
        ArgumentNullException.ThrowIfNull(pettyCashRequests);

        var aging = CalculateAging(asOfDate, obligations);
        var pettyCash = CalculatePettyCash(asOfDate, pettyCashRequests);
        var managementFee = managementFeePolicy is null || managementFeePolicy.EffectiveFrom > asOfDate
            ? null
            : decimal.Round(
                recognizedSpend * managementFeePolicy.RatePercent / 100m,
                2,
                MidpointRounding.AwayFromZero);

        return new FinanceControlCalculation(
            CalculationVersion,
            asOfDate,
            aging,
            pettyCash,
            managementFeePolicy?.Id,
            managementFeePolicy?.RatePercent,
            managementFee);
    }

    private static ObligationAgingCalculation CalculateAging(
        DateOnly asOfDate,
        IEnumerable<FinancialObligationEntry> source)
    {
        var open = source
            .Where(item => item.IssueDate <= asOfDate && item.OutstandingAmount > 0)
            .ToArray();
        var payable = open.Where(item => item.Type == FinancialObligationType.Payable).ToArray();
        var receivable = open.Where(item => item.Type == FinancialObligationType.Receivable).ToArray();

        return new ObligationAgingCalculation(
            payable.Length,
            receivable.Length,
            payable.Count(item => item.DueDate < asOfDate),
            receivable.Count(item => item.DueDate < asOfDate),
            payable.Sum(item => item.OutstandingAmount),
            receivable.Sum(item => item.OutstandingAmount),
            payable.Where(item => item.DueDate < asOfDate).Sum(item => item.OutstandingAmount),
            receivable.Where(item => item.DueDate < asOfDate).Sum(item => item.OutstandingAmount),
            Bucket(open, asOfDate, 1, 30),
            Bucket(open, asOfDate, 31, 60),
            Bucket(open, asOfDate, 61, int.MaxValue));
    }

    private static decimal Bucket(
        IEnumerable<FinancialObligationEntry> source,
        DateOnly asOfDate,
        int minimumDays,
        int maximumDays) => source
        .Where(item => item.DueDate < asOfDate)
        .Where(item =>
        {
            var days = asOfDate.DayNumber - item.DueDate.DayNumber;
            return days >= minimumDays && days <= maximumDays;
        })
        .Sum(item => item.OutstandingAmount);

    private static PettyCashControlCalculation CalculatePettyCash(
        DateOnly asOfDate,
        IEnumerable<PettyCashControlEntry> source)
    {
        var active = source.Where(item => item.Status is not PettyCashRequestStatus.Draft and
            not PettyCashRequestStatus.Returned and not PettyCashRequestStatus.Reconciled).ToArray();
        var advanced = active.Where(item => item.Status is PettyCashRequestStatus.Advanced or
            PettyCashRequestStatus.ReconciliationSubmitted).ToArray();

        return new PettyCashControlCalculation(
            active.Length,
            advanced.Length,
            advanced.Count(item => item.ReconciliationDueDate < asOfDate),
            advanced.Sum(item => item.ApprovedAmount ?? 0m),
            advanced.Where(item => item.ReconciliationDueDate < asOfDate)
                .Sum(item => item.ApprovedAmount ?? 0m));
    }
}

public sealed record FinancialObligationEntry(
    Guid Id,
    FinancialObligationType Type,
    DateOnly IssueDate,
    DateOnly DueDate,
    decimal Amount,
    decimal SettledAmount)
{
    public decimal OutstandingAmount => Amount - SettledAmount;
}

public sealed record PettyCashControlEntry(
    Guid Id,
    DateOnly ReconciliationDueDate,
    decimal? ApprovedAmount,
    PettyCashRequestStatus Status);

public sealed record ApprovedManagementFeePolicy(
    Guid Id,
    decimal RatePercent,
    ManagementFeeBase CalculationBase,
    DateOnly EffectiveFrom);

public sealed record ObligationAgingCalculation(
    int OpenPayableCount,
    int OpenReceivableCount,
    int OverduePayableCount,
    int OverdueReceivableCount,
    decimal OpenPayableAmount,
    decimal OpenReceivableAmount,
    decimal OverduePayableAmount,
    decimal OverdueReceivableAmount,
    decimal AgingOneToThirtyAmount,
    decimal AgingThirtyOneToSixtyAmount,
    decimal AgingOverSixtyAmount);

public sealed record PettyCashControlCalculation(
    int ActiveRequestCount,
    int AdvancedRequestCount,
    int OverdueReconciliationCount,
    decimal OutstandingAdvanceAmount,
    decimal OverdueAdvanceAmount);

public sealed record FinanceControlCalculation(
    string CalculationVersion,
    DateOnly AsOfDate,
    ObligationAgingCalculation Aging,
    PettyCashControlCalculation PettyCash,
    Guid? ManagementFeePolicyId,
    decimal? ManagementFeeRatePercent,
    decimal? ManagementFeeAmount);
