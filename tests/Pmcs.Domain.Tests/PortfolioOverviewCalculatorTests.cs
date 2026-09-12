using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class PortfolioOverviewCalculatorTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 9);
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyPortfolioProducesExplicitZeroState()
    {
        var result = PortfolioOverviewCalculator.Calculate([]);

        Assert.Equal(PortfolioOverviewCalculator.ContractVersion, result.ContractVersion);
        Assert.Equal(0, result.ProjectCount);
        Assert.Equal(0, result.StableProjectCount);
        Assert.Null(result.LatestSnapshotAt);
        Assert.Empty(result.CurrencyExposures);
    }

    [Fact]
    public void MissingOperationalSnapshotIsNoDataAndNeverStable()
    {
        var result = PortfolioOverviewCalculator.Calculate([
            Project(hasSnapshot: false, operationalStatus: null),
            Project(hasSnapshot: true, operationalStatus: ProjectOperationalStatus.Stable)
        ]);

        Assert.Equal(1, result.NoDataProjectCount);
        Assert.Equal(1, result.StableProjectCount);
    }

    [Fact]
    public void CurrencyExposureIsSeparatedAndNotConfiguredSourcesAreExcluded()
    {
        var result = PortfolioOverviewCalculator.Calculate([
            Project(financial: Financial("IRR", FinancialStateStatus.Available, recognizedSpend: 120m)),
            Project(commercial: Commercial("USD", CommercialMetricState.Available, committed: 80m, open: 30m)),
            Project(
                financial: Financial("IRR", FinancialStateStatus.NotConfigured, recognizedSpend: 999m),
                commercial: Commercial("USD", CommercialMetricState.NotConfigured, committed: 999m, open: 999m))
        ]);

        Assert.Collection(
            result.CurrencyExposures,
            irr =>
            {
                Assert.Equal("IRR", irr.CurrencyCode);
                Assert.Equal(1, irr.FinancialProjectCount);
                Assert.Equal(120m, irr.RecognizedSpend);
                Assert.Equal(0m, irr.TotalCommittedAmount);
            },
            usd =>
            {
                Assert.Equal("USD", usd.CurrencyCode);
                Assert.Equal(1, usd.CommercialProjectCount);
                Assert.Equal(80m, usd.TotalCommittedAmount);
                Assert.Equal(30m, usd.OpenCommitmentAmount);
            });
    }

    private static PortfolioProjectOverviewInput Project(
        bool hasSnapshot = false,
        ProjectOperationalStatus? operationalStatus = null,
        FinancialStateRecord? financial = null,
        CommercialStateRecord? commercial = null) => new(
        Guid.NewGuid(),
        ProjectStatus.Active,
        hasSnapshot,
        operationalStatus,
        hasSnapshot ? DataFreshnessStatus.Current : null,
        IsOutdated: false,
        ActionsVisible: false,
        OpenActionCount: 0,
        OverdueActionCount: 0,
        financial,
        commercial,
        LatestSnapshotAt: hasSnapshot ? Now : null);

    private static FinancialStateRecord Financial(
        string currency,
        FinancialStateStatus status,
        decimal recognizedSpend) => new(
        SnapshotId: status == FinancialStateStatus.Available ? Guid.NewGuid() : null,
        CalculationVersion: "financial-state-v1",
        AsOfDate: AsOf,
        CalculatedAt: Now,
        CurrencyCode: currency,
        Status: status,
        DataQualityStatus: status == FinancialStateStatus.Available
            ? FinancialDataQualityStatus.Adequate
            : FinancialDataQualityStatus.NoData,
        BudgetComparisonState: BudgetComparisonState.NotConfigured,
        PostedRecordCount: status == FinancialStateStatus.Available ? 1 : 0,
        TotalReceipts: 0,
        DirectPayments: recognizedSpend,
        PettyCashFunding: 0,
        PettyCashExpenses: 0,
        ExternalNetCash: -recognizedSpend,
        RecognizedSpend: recognizedSpend,
        PettyCashBalance: 0,
        ApprovedBudgetAmount: null,
        BudgetRemainingAmount: null,
        BudgetConsumedPercent: null,
        SourceMaxChangedAt: status == FinancialStateStatus.Available ? Now : null);

    private static CommercialStateRecord Commercial(
        string currency,
        CommercialMetricState procurementState,
        decimal committed,
        decimal open) => new(
        SnapshotId: procurementState == CommercialMetricState.Available ? Guid.NewGuid() : null,
        CalculationVersion: "commercial-state-v1",
        AsOfDate: AsOf,
        CalculatedAt: Now,
        CurrencyCode: currency,
        ContractState: CommercialMetricState.NotConfigured,
        ContractDataQualityStatus: CommercialDataQualityStatus.NoData,
        ProcurementState: procurementState,
        ProcurementDataQualityStatus: procurementState == CommercialMetricState.Available
            ? CommercialDataQualityStatus.Adequate
            : CommercialDataQualityStatus.NoData,
        ActivePartyCount: 0,
        RegisteredContractCount: 0,
        ActiveContractCount: 0,
        ContractsWithoutCeilingCount: 0,
        PendingContractApprovalCount: 0,
        ExpiredActiveContractCount: 0,
        ApprovedAmendmentCount: 0,
        ApprovedAmendmentDelta: 0,
        ApprovedContractCeilingAmount: null,
        PurchaseRequestCount: 1,
        PendingProcurementApprovalCount: 0,
        ApprovedRequestsAwaitingOrderCount: 0,
        OpenCommitmentCount: open > 0 ? 1 : 0,
        OverdueCommitmentCount: 0,
        TotalCommittedAmount: committed,
        OpenCommitmentAmount: open,
        SourceMaxChangedAt: procurementState == CommercialMetricState.Available ? Now : null);
}
