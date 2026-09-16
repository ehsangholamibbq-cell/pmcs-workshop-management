using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Domain;

namespace Pmcs.Domain.Tests;

public sealed class FinanceControlWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 9, 16);

    [Fact]
    public void PayableRequiresApprovalAndCannotBeOverSettled()
    {
        var item = Obligation(FinancialObligationType.Payable, 1_000m, Today.AddDays(10));

        Assert.Throws<DomainRuleException>(() => item.ApplySettlement(1, 100m, Now));
        item.Submit(1, Now);
        item.Approve(2, "verified", Guid.NewGuid(), Now);
        item.ApplySettlement(3, 400m, Now);

        Assert.Equal(FinancialObligationStatus.PartiallySettled, item.Status);
        Assert.Equal(600m, item.OutstandingAmount);
        Assert.Throws<DomainRuleException>(() => item.ApplySettlement(4, 601m, Now));

        item.ApplySettlement(4, 600m, Now);
        Assert.Equal(FinancialObligationStatus.Settled, item.Status);
        Assert.Equal(0m, item.OutstandingAmount);
    }

    [Fact]
    public void ObligationAgingUsesDueDateAndOutstandingAmount()
    {
        var result = FinanceControlCalculator.Calculate(
            Today,
            2_000m,
            [
                new FinancialObligationEntry(Guid.NewGuid(), FinancialObligationType.Payable, Today.AddDays(-50), Today.AddDays(-35), 1_000m, 200m),
                new FinancialObligationEntry(Guid.NewGuid(), FinancialObligationType.Receivable, Today.AddDays(-10), Today.AddDays(-5), 700m, 0m),
                new FinancialObligationEntry(Guid.NewGuid(), FinancialObligationType.Payable, Today, Today.AddDays(5), 300m, 0m)
            ],
            [],
            null);

        Assert.Equal(2, result.Aging.OpenPayableCount);
        Assert.Equal(1, result.Aging.OverduePayableCount);
        Assert.Equal(1_100m, result.Aging.OpenPayableAmount);
        Assert.Equal(700m, result.Aging.OverdueReceivableAmount);
        Assert.Equal(800m, result.Aging.AgingThirtyOneToSixtyAmount);
        Assert.Null(result.ManagementFeeAmount);
    }

    [Fact]
    public void ManagementFeeIsOptionalEffectiveDatedAndDeterministic()
    {
        var future = FinanceControlCalculator.Calculate(
            Today,
            2_000m,
            [],
            [],
            new ApprovedManagementFeePolicy(Guid.NewGuid(), 7.5m, ManagementFeeBase.RecognizedSpend, Today.AddDays(1)));
        var active = FinanceControlCalculator.Calculate(
            Today,
            2_000m,
            [],
            [],
            new ApprovedManagementFeePolicy(Guid.NewGuid(), 7.5m, ManagementFeeBase.RecognizedSpend, Today));

        Assert.Null(future.ManagementFeeAmount);
        Assert.Equal(150m, active.ManagementFeeAmount);
    }

    [Fact]
    public void PettyCashAdvanceAndReconciliationAreSeparateBalancedTransitions()
    {
        var item = PettyCashRequest.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "PC-01", "Site supplies", "Cashier",
            Today, Today.AddDays(7), 1_000m, "IRR", Guid.NewGuid(), "ZONE-A", "CC-1", "WBS-1",
            Guid.NewGuid(), Now);

        item.Submit(1, Now);
        item.Approve(2, 900m, null, Guid.NewGuid(), Now);
        item.RecordAdvance(3, Guid.NewGuid(), Now);

        Assert.Throws<DomainRuleException>(() => item.SubmitReconciliation(
            4, 700m, 100m, Guid.NewGuid(), Guid.NewGuid(), Now));

        item.SubmitReconciliation(4, 700m, 200m, Guid.NewGuid(), Guid.NewGuid(), Now);
        item.ApproveReconciliation(5, "documents checked", Guid.NewGuid(), Now);

        Assert.Equal(PettyCashRequestStatus.Reconciled, item.Status);
        Assert.Equal(700m, item.ReconciledExpenseAmount);
        Assert.Equal(200m, item.ReturnedAmount);
    }

    [Fact]
    public void OverduePettyCashIsVisibleWithoutChangingLedgerSpend()
    {
        var result = FinanceControlCalculator.Calculate(
            Today,
            500m,
            [],
            [new PettyCashControlEntry(Guid.NewGuid(), Today.AddDays(-1), 250m, PettyCashRequestStatus.Advanced)],
            null);

        Assert.Equal(1, result.PettyCash.OverdueReconciliationCount);
        Assert.Equal(250m, result.PettyCash.OutstandingAdvanceAmount);
    }

    [Fact]
    public void ManagementFeePolicyHasExplicitApprovalAndSupersedeStates()
    {
        var item = ManagementFeePolicy.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CM fee", 5m,
            ManagementFeeBase.RecognizedSpend, Today, null, Guid.NewGuid(), Now);

        Assert.Throws<DomainRuleException>(() => item.Approve(1, null, Guid.NewGuid(), Now));
        item.Submit(1, Now);
        item.Approve(2, null, Guid.NewGuid(), Now);
        item.Supersede(Guid.NewGuid(), Now);

        Assert.Equal(ManagementFeePolicyStatus.Superseded, item.Status);
        Assert.Equal(4, item.Revision);
    }

    private static FinancialObligation Obligation(
        FinancialObligationType type,
        decimal amount,
        DateOnly dueDate) => FinancialObligation.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type, "OB-01", "Verified obligation",
        Today, dueDate, amount, "IRR", null, "Counterparty", null, null, "CC-1", "WBS-1",
        Guid.NewGuid(), "ZONE-A", Guid.NewGuid(), Now);
}
