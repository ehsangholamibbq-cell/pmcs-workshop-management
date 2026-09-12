using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Domain;

namespace Pmcs.Domain.Tests;

public sealed class BudgetBaselineWorkflowTests
{
    [Fact]
    public void BudgetBaselineIsOptionalButApprovalIsAControlledWorkflow()
    {
        var baseline = BudgetBaseline.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Initial management budget",
            10_000_000m,
            "IRR",
            null,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleException>(() =>
            baseline.Approve(1, null, Guid.NewGuid(), DateTimeOffset.UtcNow));

        baseline.Submit(1, DateTimeOffset.UtcNow);
        baseline.Approve(2, "approved", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(BudgetBaselineStatus.Approved, baseline.Status);
        Assert.Equal(3, baseline.Revision);
    }

    [Fact]
    public void ApprovedBaselineCanBeSupersededWithoutChangingItsAmount()
    {
        var baseline = BudgetBaseline.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Version one", 500m, "IRR", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        baseline.Submit(1, DateTimeOffset.UtcNow);
        baseline.Approve(2, null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        baseline.Supersede(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(BudgetBaselineStatus.Superseded, baseline.Status);
        Assert.Equal(500m, baseline.Amount);
    }

    [Fact]
    public void ReturnedBaselineCanBeAmendedBeforeResubmission()
    {
        var baseline = BudgetBaseline.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Draft", 500m, "IRR", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        baseline.Submit(1, DateTimeOffset.UtcNow);
        baseline.ReturnForCorrection(2, "revise", Guid.NewGuid(), DateTimeOffset.UtcNow);

        baseline.Amend(3, "Revised", 750m, "IRR", "updated scope");
        baseline.Submit(4, DateTimeOffset.UtcNow);

        Assert.Equal(750m, baseline.Amount);
        Assert.Equal(BudgetBaselineStatus.Submitted, baseline.Status);
    }
}
