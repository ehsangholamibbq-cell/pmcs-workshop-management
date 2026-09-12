using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class FinancialStateCalculatorTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 9);
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MissingBudgetDoesNotBlockFinancialStateOrCreateZeroVariance()
    {
        var entries = new[]
        {
            Entry(FinancialRecordType.Receipt, 200m),
            Entry(FinancialRecordType.Payment, 100m),
            Entry(FinancialRecordType.PettyCashFunding, 50m),
            Entry(FinancialRecordType.PettyCashExpense, 30m)
        };

        var state = FinancialStateCalculator.Calculate(Profile(), entries, null, AsOf, Now);

        Assert.Equal(FinancialStateStatus.Available, state.Status);
        Assert.Equal(BudgetComparisonState.SetupRequired, state.BudgetComparisonState);
        Assert.Equal(50m, state.ExternalNetCash);
        Assert.Equal(130m, state.RecognizedSpend);
        Assert.Equal(20m, state.PettyCashBalance);
        Assert.Null(state.ApprovedBudgetAmount);
        Assert.Null(state.BudgetConsumedPercent);
    }

    [Fact]
    public void NoPostedFactsIsNoDataRatherThanHealthy()
    {
        var state = FinancialStateCalculator.Calculate(Profile(), [], null, AsOf, Now);

        Assert.Equal(FinancialStateStatus.NoData, state.Status);
        Assert.Equal(FinancialDataQualityStatus.NoData, state.DataQualityStatus);
        Assert.Equal(0, state.PostedRecordCount);
    }

    [Fact]
    public void ApprovedBudgetEnablesExplicitComparison()
    {
        var state = FinancialStateCalculator.Calculate(
            Profile(),
            [Entry(FinancialRecordType.Payment, 250m)],
            new ApprovedBudget(Guid.NewGuid(), 1_000m, "IRR", Now),
            AsOf,
            Now);

        Assert.Equal(BudgetComparisonState.Available, state.BudgetComparisonState);
        Assert.Equal(750m, state.BudgetRemainingAmount);
        Assert.Equal(25m, state.BudgetConsumedPercent);
    }

    [Fact]
    public void PettyCashExpenseWithoutFundingIsFlaggedAsDataQualityIssue()
    {
        var state = FinancialStateCalculator.Calculate(
            Profile(),
            [Entry(FinancialRecordType.PettyCashExpense, 50m)],
            null,
            AsOf,
            Now);

        Assert.Equal(FinancialDataQualityStatus.NeedsAttention, state.DataQualityStatus);
        Assert.Equal(-50m, state.PettyCashBalance);
    }

    [Fact]
    public void DisabledFinanceIsExplicitlyNotConfigured()
    {
        var profile = Profile() with { Finance = ProjectFeatureState.NotEnabled };

        var state = FinancialStateCalculator.Calculate(profile, [], null, AsOf, Now);

        Assert.Equal(FinancialStateStatus.NotConfigured, state.Status);
    }

    private static PostedFinancialEntry Entry(FinancialRecordType type, decimal amount) => new(
        Guid.NewGuid(), type, AsOf, amount, "IRR", Now);

    private static ProjectControlProfile Profile() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "FIN-01",
        "Finance project",
        "Asia/Tehran",
        "IRR",
        1,
        null,
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.None,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.SetupRequired,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null));
}
