using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class CommercialStateCalculatorTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 9);
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MissingRegistersRemainNoDataRatherThanHealthy()
    {
        var state = Calculate(Profile(), [], [], [], []);

        Assert.Equal(CommercialMetricState.NoData, state.ContractState);
        Assert.Equal(CommercialMetricState.NoData, state.ProcurementState);
        Assert.Equal(CommercialDataQualityStatus.NoData, state.ContractDataQualityStatus);
        Assert.Null(state.ApprovedContractCeilingAmount);
    }

    [Fact]
    public void ContractWithoutCeilingStaysExplicitlyUnknown()
    {
        var contract = new CommercialContractFact(
            Guid.NewGuid(), ProjectContractStatus.Active, null, "IRR", null, Now);

        var state = Calculate(Profile(), [contract], [], [], []);

        Assert.Equal(CommercialMetricState.Available, state.ContractState);
        Assert.Equal(1, state.ContractsWithoutCeilingCount);
        Assert.Null(state.ApprovedContractCeilingAmount);
    }

    [Fact]
    public void ApprovedAmendmentChangesKnownContractCeiling()
    {
        var contractId = Guid.NewGuid();
        var contract = new CommercialContractFact(
            contractId, ProjectContractStatus.Active, 1_000m, "IRR", null, Now);
        var amendment = new CommercialAmendmentFact(
            Guid.NewGuid(), contractId, ContractAmendmentStatus.Approved, 250m, "IRR", Now);

        var state = Calculate(Profile(), [contract], [amendment], [], []);

        Assert.Equal(1_250m, state.ApprovedContractCeilingAmount);
        Assert.Equal(250m, state.ApprovedAmendmentDelta);
    }

    [Fact]
    public void ExpiredContractAndOverdueCommitmentAreAttentionNotCompositeHealth()
    {
        var contract = new CommercialContractFact(
            Guid.NewGuid(), ProjectContractStatus.Active, 1_000m, "IRR", AsOf.AddDays(-1), Now);
        var order = new CommercialOrderFact(
            Guid.NewGuid(), PurchaseOrderStatus.Issued, 400m, "IRR", AsOf.AddDays(-1), Now);

        var state = Calculate(Profile(), [contract], [], [], [order]);

        Assert.Equal(CommercialDataQualityStatus.NeedsAttention, state.ContractDataQualityStatus);
        Assert.Equal(CommercialDataQualityStatus.NeedsAttention, state.ProcurementDataQualityStatus);
        Assert.Equal(1, state.ExpiredActiveContractCount);
        Assert.Equal(1, state.OverdueCommitmentCount);
    }

    [Fact]
    public void DisabledProcurementIsNotConfiguredEvenWhenThereIsNoData()
    {
        var state = Calculate(Profile() with { Procurement = ProjectFeatureState.NotEnabled }, [], [], [], []);

        Assert.Equal(CommercialMetricState.NotConfigured, state.ProcurementState);
    }

    private static CommercialStateCalculation Calculate(
        ProjectControlProfile profile,
        IReadOnlyCollection<CommercialContractFact> contracts,
        IReadOnlyCollection<CommercialAmendmentFact> amendments,
        IReadOnlyCollection<CommercialRequestFact> requests,
        IReadOnlyCollection<CommercialOrderFact> orders) =>
        CommercialStateCalculator.Calculate(profile, contracts, amendments, requests, orders, 0, AsOf, Now);

    private static ProjectControlProfile Profile() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "COM-01",
        "Commercial project",
        "Asia/Tehran",
        "IRR",
        1,
        null,
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.None,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null));
}
