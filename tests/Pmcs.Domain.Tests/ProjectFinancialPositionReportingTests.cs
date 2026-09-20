using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Finance.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectFinancialPositionReportingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 20, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 20);

    [Fact]
    public void PostedAndTransactionDatesBothBoundOfficialCashSelection()
    {
        var result = Calculate(Projection(records:
        [
            Record(10, FinancialRecordType.Receipt, 100m),
            Record(11, FinancialRecordType.Receipt, 200m, postedAt: Cutoff.AddMinutes(1)),
            Record(12, FinancialRecordType.Receipt, 300m, transactionDate: CutoffLocalDate.AddDays(1))
        ]));

        Assert.Equal(ProjectFinancialPositionSectionStatus.Available, result.CashStatus);
        Assert.Equal(100m, result.Cash.TotalReceipts);
        Assert.Equal(1, result.SourceCounts.OfficialFinancialRecordCount);
        Assert.Equal(2, result.SourceCounts.ExcludedFinancialRecordCount);
    }

    [Fact]
    public void DraftSubmittedAndReturnedRecordsNeverBecomeOfficialCash()
    {
        var result = Calculate(Projection(records:
        [
            Record(10, FinancialRecordType.Receipt, 100m, FinancialRecordStatus.Draft),
            Record(11, FinancialRecordType.Payment, 100m, FinancialRecordStatus.Submitted),
            Record(12, FinancialRecordType.PettyCashExpense, 100m, FinancialRecordStatus.Returned)
        ]));

        Assert.Equal(ProjectFinancialPositionDataStatus.NoData, result.DataStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.NoData, result.CashStatus);
        Assert.Null(result.Cash.TotalReceipts);
        Assert.Contains(ProjectFinancialPositionReasonCode.OfficialFinancialRecordsMissing, result.ReasonCodes);
    }

    [Fact]
    public void OnlySettlementAtOrBeforeCutoffReducesOutstandingAmount()
    {
        var payment = Record(10, FinancialRecordType.Payment, 100m);
        var obligation = Obligation(20, FinancialObligationType.Payable, 100m);
        var result = Calculate(Projection(
            records: [payment],
            obligations: [obligation],
            settlements:
            [
                Settlement(30, obligation.ObligationId, payment.RecordId, 30m, Cutoff.AddDays(-1)),
                Settlement(31, obligation.ObligationId, payment.RecordId, 20m, Cutoff.AddDays(1))
            ]));

        var row = Assert.Single(result.OpenObligations);
        Assert.Equal(30m, row.SettledAmountAtCutoff);
        Assert.Equal(70m, row.OutstandingAmount);
        Assert.Equal(1, result.SourceCounts.EligibleSettlementCount);
    }

    [Fact]
    public void BudgetLifecycleSelectsBaselineEffectiveAtCutoff()
    {
        var historical = Budget(40, 1_000m, Cutoff.AddDays(-10), Cutoff.AddDays(1));
        var future = Budget(41, 2_000m, Cutoff.AddDays(2));
        var result = Calculate(Projection(
            records: [Record(10, FinancialRecordType.Payment, 100m)],
            budgets: [future, historical]));

        Assert.Equal(historical.BaselineId, result.Budget?.BaselineId);
        Assert.Equal(1_000m, result.Budget?.Amount);
        Assert.Equal(1, result.SourceCounts.EffectiveBudgetBaselineCount);
    }

    [Fact]
    public void InactiveAndSuspendedFinanceAreNotConfiguredWithoutSyntheticMetrics()
    {
        foreach (var state in new[]
        {
            ProjectFeatureState.NotEnabled,
            ProjectFeatureState.Suspended
        })
        {
            var result = Calculate(Projection(
                records: [Record(10, FinancialRecordType.Receipt, 100m)],
                financeState: state));

            Assert.Equal(ProjectFinancialPositionDataStatus.NotConfigured, result.DataStatus);
            Assert.Equal(ProjectFinancialPositionSectionStatus.NotConfigured, result.CashStatus);
            Assert.Null(result.Cash.TotalReceipts);
            Assert.Null(result.Budget);
        }
    }

    [Fact]
    public void ActiveFinanceWithoutOfficialEvidenceIsNoDataNotZero()
    {
        var result = Calculate(Projection());

        Assert.Equal(ProjectFinancialPositionDataStatus.NoData, result.DataStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.NoData, result.CashStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.NoData, result.ObligationStatus);
        Assert.Null(result.Cash.RecognizedSpend);
        Assert.Null(result.PayableSummary);
        Assert.Empty(result.Aging);
    }

    [Fact]
    public void IncompleteLedgerNullsCashAndMakesSnapshotInsufficient()
    {
        var result = Calculate(Projection(
            records: [Record(10, FinancialRecordType.Receipt, 100m)],
            ledger: ProjectFinancialSourceCompleteness.Incomplete));

        Assert.Equal(ProjectFinancialPositionDataStatus.InsufficientData, result.DataStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.InsufficientData, result.CashStatus);
        Assert.Null(result.Cash.TotalReceipts);
        Assert.Contains(ProjectFinancialPositionReasonCode.FinancialSourceIncomplete, result.ReasonCodes);
    }

    [Fact]
    public void IncompleteSettlementLineagePreservesCashButNullsObligations()
    {
        var result = Calculate(Projection(
            records: [Record(10, FinancialRecordType.Receipt, 100m)],
            obligations: [Obligation(20, FinancialObligationType.Payable, 80m)],
            settlementLineage: ProjectFinancialSourceCompleteness.Incomplete));

        Assert.Equal(ProjectFinancialPositionDataStatus.InsufficientData, result.DataStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.Available, result.CashStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.InsufficientData, result.ObligationStatus);
        Assert.Equal(100m, result.Cash.TotalReceipts);
        Assert.Empty(result.OpenObligations);
        Assert.Contains(
            ProjectFinancialPositionReasonCode.ObligationSettlementLineageIncomplete,
            result.ReasonCodes);
    }

    [Fact]
    public void AllFourFinancialRecordTypesProduceTheSevenCanonicalCashMetrics()
    {
        var result = Calculate(Projection(records:
        [
            Record(10, FinancialRecordType.Receipt, 500m),
            Record(11, FinancialRecordType.Payment, 120m),
            Record(12, FinancialRecordType.PettyCashFunding, 80m),
            Record(13, FinancialRecordType.PettyCashExpense, 30m)
        ]));

        Assert.Equal(500m, result.Cash.TotalReceipts);
        Assert.Equal(120m, result.Cash.DirectPayments);
        Assert.Equal(80m, result.Cash.PettyCashFunding);
        Assert.Equal(30m, result.Cash.PettyCashExpenses);
        Assert.Equal(300m, result.Cash.ExternalNetCash);
        Assert.Equal(150m, result.Cash.RecognizedSpend);
        Assert.Equal(50m, result.Cash.PettyCashBalance);
    }

    [Fact]
    public void PettyCashFundingAndExpenseAreNotDoubleCounted()
    {
        var result = Calculate(Projection(records:
        [
            Record(10, FinancialRecordType.PettyCashFunding, 100m),
            Record(11, FinancialRecordType.PettyCashExpense, 25m)
        ]));

        Assert.Equal(-100m, result.Cash.ExternalNetCash);
        Assert.Equal(25m, result.Cash.RecognizedSpend);
        Assert.Equal(75m, result.Cash.PettyCashBalance);
    }

    [Fact]
    public void BudgetNotConfiguredDoesNotDowngradeAvailableCash()
    {
        var result = Calculate(Projection(
            records: [Record(10, FinancialRecordType.Receipt, 100m)],
            budgetState: ProjectFeatureState.NotConfigured));

        Assert.Equal(ProjectFinancialPositionDataStatus.Available, result.DataStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.NotConfigured, result.BudgetStatus);
        Assert.Equal(ProjectFinancialPositionSectionStatus.NotConfigured, result.BudgetComparisonStatus);
        Assert.Null(result.Budget);
        Assert.Contains(ProjectFinancialPositionReasonCode.BudgetNotConfigured, result.ReasonCodes);
    }

    [Fact]
    public void AvailableBudgetComparisonUsesDeterministicAwayFromZeroRounding()
    {
        var result = Calculate(Projection(
            records: [Record(10, FinancialRecordType.Payment, 100m)],
            budgets: [Budget(40, 300m, Cutoff.AddDays(-10))]));

        Assert.Equal(200m, result.BudgetComparison?.BudgetRemainingAmount);
        Assert.Equal(33.3m, result.BudgetComparison?.BudgetConsumedPercent);
        Assert.Equal(ProjectFinancialPositionSectionStatus.Available, result.BudgetComparisonStatus);
    }

    [Fact]
    public void SpendAboveBudgetPreservesNegativeRemainingAndPercentAboveOneHundred()
    {
        var result = Calculate(Projection(
            records: [Record(10, FinancialRecordType.Payment, 125m)],
            budgets: [Budget(40, 100m, Cutoff.AddDays(-10))]));

        Assert.Equal(-25m, result.BudgetComparison?.BudgetRemainingAmount);
        Assert.Equal(125.0m, result.BudgetComparison?.BudgetConsumedPercent);
    }

    [Fact]
    public void PayableAndReceivableTotalsAndAgingNeverNet()
    {
        var result = Calculate(Projection(obligations:
        [
            Obligation(20, FinancialObligationType.Payable, 100m, dueDate: CutoffLocalDate.AddDays(-10)),
            Obligation(21, FinancialObligationType.Receivable, 70m, dueDate: CutoffLocalDate.AddDays(-40))
        ]));

        Assert.Equal(100m, result.PayableSummary?.OpenAmount);
        Assert.Equal(70m, result.ReceivableSummary?.OpenAmount);
        Assert.Equal(100m, result.PayableSummary?.OverdueAmount);
        Assert.Equal(70m, result.ReceivableSummary?.OverdueAmount);
        Assert.Equal(8, result.Aging.Count);
    }

    [Fact]
    public void MultiplePartialAndFullSettlementsUseImmutableRowsAtCutoff()
    {
        var paymentOne = Record(10, FinancialRecordType.Payment, 100m);
        var paymentTwo = Record(11, FinancialRecordType.Payment, 50m);
        var partial = Obligation(20, FinancialObligationType.Payable, 100m);
        var full = Obligation(21, FinancialObligationType.Payable, 50m);
        var result = Calculate(Projection(
            records: [paymentOne, paymentTwo],
            obligations: [partial, full],
            settlements:
            [
                Settlement(30, partial.ObligationId, paymentOne.RecordId, 20m),
                Settlement(31, partial.ObligationId, paymentOne.RecordId, 30m),
                Settlement(32, full.ObligationId, paymentTwo.RecordId, 50m)
            ]));

        var row = Assert.Single(result.OpenObligations);
        Assert.Equal(partial.ObligationId, row.ObligationId);
        Assert.Equal(50m, row.SettledAmountAtCutoff);
        Assert.Equal(50m, row.OutstandingAmount);
        Assert.Equal(1, result.PayableSummary?.OpenCount);
    }

    [Fact]
    public void DueExactlyOnCutoffIsNotDueAndNotOverdue()
    {
        var result = Calculate(Projection(obligations:
        [Obligation(20, FinancialObligationType.Payable, 100m, dueDate: CutoffLocalDate)]));

        var row = Assert.Single(result.OpenObligations);
        Assert.Equal(ProjectFinancialPositionAgingBucket.NotDue, row.Bucket);
        Assert.Equal(0, result.PayableSummary?.OverdueCount);
    }

    [Fact]
    public void AgingBoundaryDaysMapToExactlyOneCanonicalBucket()
    {
        var days = new[] { 0, 1, 30, 31, 60, 61 };
        var result = Calculate(Projection(obligations: days
            .Select((day, index) => Obligation(
                20 + index,
                FinancialObligationType.Payable,
                10m,
                dueDate: CutoffLocalDate.AddDays(-day)))
            .ToArray()));

        Assert.Equal(6, result.OpenObligations.Count);
        Assert.Equal(1, result.Aging.Single(item => item.Type == FinancialObligationType.Payable &&
            item.Bucket == ProjectFinancialPositionAgingBucket.NotDue).Count);
        Assert.Equal(2, result.Aging.Single(item => item.Type == FinancialObligationType.Payable &&
            item.Bucket == ProjectFinancialPositionAgingBucket.Overdue1To30).Count);
        Assert.Equal(2, result.Aging.Single(item => item.Type == FinancialObligationType.Payable &&
            item.Bucket == ProjectFinancialPositionAgingBucket.Overdue31To60).Count);
        Assert.Equal(1, result.Aging.Single(item => item.Type == FinancialObligationType.Payable &&
            item.Bucket == ProjectFinancialPositionAgingBucket.Overdue61Plus).Count);
    }

    [Fact]
    public void EligibleCurrencyMismatchFailsClosedWithoutFxOrSilentExclusion()
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            Select(Projection(
                records: [Record(10, FinancialRecordType.Receipt, 100m, currency: "USD")])));

        Assert.Equal("finance.financial_position_reporting.record.currency_mismatch", exception.Code);
    }

    [Fact]
    public void OverlappingBudgetBaselinesFailClosedWithoutLatestTieBreak()
    {
        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(budgets:
        [
            Budget(40, 100m, Cutoff.AddDays(-10)),
            Budget(41, 200m, Cutoff.AddDays(-5))
        ])));

        Assert.Equal("finance.financial_position_reporting.budget.overlap", exception.Code);
    }

    [Fact]
    public void TwinRunsIgnoreQueryOrderRunIdentityAndBuildTime()
    {
        var first = Calculate(Projection(
            records:
            [
                Record(11, FinancialRecordType.Payment, 20m),
                Record(10, FinancialRecordType.Receipt, 100m)
            ],
            obligations:
            [
                Obligation(21, FinancialObligationType.Receivable, 30m),
                Obligation(20, FinancialObligationType.Payable, 40m)
            ]));
        var second = Calculate(Projection(
            records:
            [
                Record(10, FinancialRecordType.Receipt, 100m),
                Record(11, FinancialRecordType.Payment, 20m)
            ],
            obligations:
            [
                Obligation(20, FinancialObligationType.Payable, 40m),
                Obligation(21, FinancialObligationType.Receivable, 30m)
            ]));
        var pinned = ProjectFinancialPositionPinnedProjectProfile.Capture(
            Profile(),
            Cutoff.AddMinutes(1));
        var firstSnapshot = Build(first, pinned, Id(500), Cutoff.AddMinutes(2));
        var secondSnapshot = Build(second, pinned, Id(501), Cutoff.AddHours(2));

        Assert.Equal(firstSnapshot.Sha256, secondSnapshot.Sha256);
        Assert.Equal(firstSnapshot.SourceManifestSha256, secondSnapshot.SourceManifestSha256);
        Assert.Equal(firstSnapshot.PayloadJson, secondSnapshot.PayloadJson);
        Assert.Equal(firstSnapshot.SourceManifestJson, secondSnapshot.SourceManifestJson);
    }

    [Fact]
    public void CrossTenantFinancialEvidenceFailsClosed()
    {
        var record = Record(10, FinancialRecordType.Receipt, 100m) with { TenantId = Id(999) };
        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(records: [record])));

        Assert.Equal("finance.financial_position_reporting.record.invalid", exception.Code);
    }

    [Fact]
    public void ClassificationIsAtLeastConfidentialAndRestrictedEvidencePropagates()
    {
        var restricted = Record(10, FinancialRecordType.Receipt, 100m) with
        {
            Classification = ProjectFinancialPositionReportingClassification.Restricted
        };
        var result = Calculate(Projection(records: [restricted]));
        var snapshot = Build(result);
        var internalSource = Assert.Throws<DomainRuleException>(() => Select(Projection(
            sourceClassification: ProjectFinancialPositionReportingClassification.Internal)));

        Assert.Equal(ProjectFinancialPositionReportingClassification.Restricted, result.Classification);
        Assert.Equal(ReportClassification.Restricted, snapshot.Classification);
        Assert.Equal("finance.financial_position_reporting.classification.invalid", internalSource.Code);
    }

    [Fact]
    public void SemanticSnapshotContainsOnlyAllowlistedFinancialMetadata()
    {
        var result = Calculate(Projection(
            records: [Record(10, FinancialRecordType.Receipt, 100m)],
            obligations: [Obligation(
                20,
                FinancialObligationType.Receivable,
                50m,
                number: "AR-001",
                counterparty: "Certified Counterparty")]));
        var payload = Build(result).PayloadJson;

        Assert.Contains("AR-001", payload, StringComparison.Ordinal);
        Assert.Contains("Certified Counterparty", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("description", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contractId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("partyId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("managementFee", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("forecast", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeIdentityUsesStrictEmptyParametersAndVersionedSchemas()
    {
        Assert.Equal("{}", CanonicalJson.Serialize(new ProjectFinancialPositionReportParameters()));
        Assert.Equal(
            "project-financial-position-certified",
            ProjectFinancialPositionReportRuntimeContract.DefinitionCode);
        Assert.Equal(
            "pmcs.reporting.project-financial-position.parameters/v1",
            ProjectFinancialPositionReportRuntimeContract.ParameterSchemaVersion);
        Assert.Equal(
            "pmcs.reporting.project-financial-position.snapshot/v1",
            ProjectFinancialPositionReportRuntimeContract.SnapshotSchemaVersion);
        Assert.Equal(
            "pmcs.finance.project-financial-position-reporting/v1",
            ProjectFinancialPositionReportingContract.Version);
    }

    [Fact]
    public void SnapshotBoundaryDependsOnlyOnVersionedFinanceResultAndPinnedProjectProfile()
    {
        var parameterTypes = typeof(ProjectFinancialPositionReportSnapshotBuilder)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
            .ToArray();

        Assert.Contains(parameterTypes, item => item.Contains(
            nameof(ProjectFinancialPositionReportingResult),
            StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypes, item => item.Contains("FinanceDbContext", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypes, item => item.Contains("IFinancialStateSource", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypes, item => item.Contains("IFinanceControlReadService", StringComparison.Ordinal));
    }

    [Fact]
    public void CompatibilityProjectionRejectsConfigurationChangedAfterCutoff()
    {
        var profile = Profile() with { ConfigurationChangedAt = Cutoff.AddMinutes(1) };
        var exception = Assert.Throws<DomainRuleException>(() =>
            ProjectFinancialPositionReportingCompatibilityProjection.Create(
                profile,
                CutoffLocalDate,
                Cutoff,
                [],
                [],
                [],
                []));

        Assert.Equal(
            "finance.financial_position_reporting.configuration_history.unavailable",
            exception.Code);
    }

    [Fact]
    public void CompatibilityProjectionRejectsLegacySupersededBudgetHistory()
    {
        var baseline = BudgetBaseline.Create(
            Id(40),
            TenantId,
            ProjectId,
            "Official Budget",
            100m,
            "IRR",
            null,
            Id(90),
            Cutoff.AddDays(-20));
        baseline.Submit(baseline.Revision, Cutoff.AddDays(-15));
        baseline.Approve(baseline.Revision, null, Id(91), Cutoff.AddDays(-10));
        baseline.Supersede(Id(92), Cutoff.AddDays(-1));

        var exception = Assert.Throws<DomainRuleException>(() =>
            ProjectFinancialPositionReportingCompatibilityProjection.Create(
                Profile(),
                CutoffLocalDate,
                Cutoff,
                [],
                [],
                [],
                [baseline]));

        Assert.Equal("finance.financial_position_reporting.budget_history.unavailable", exception.Code);
    }

    [Fact]
    public void FutureCutoffAndUnknownTimeZoneFailAtPinnedSnapshotBoundary()
    {
        var result = Calculate(Projection(records: [Record(10, FinancialRecordType.Receipt, 100m)]));
        var future = Assert.Throws<DomainRuleException>(() =>
            ProjectFinancialPositionReportSnapshotBuilder.Build(
                Id(500),
                TenantId,
                ProjectFinancialPositionPinnedProjectProfile.Capture(Profile(), Cutoff.AddMinutes(1)),
                Cutoff.AddMinutes(2),
                result with { SourceCutoffUtc = Cutoff.AddMinutes(2) },
                Cutoff.AddMinutes(1),
                Cutoff.AddMinutes(3)));
        var badZone = ProjectFinancialPositionPinnedProjectProfile.Capture(Profile(), Cutoff.AddMinutes(1)) with
        {
            TimeZone = "Iran/Unknown-City"
        };
        var timeZone = Assert.Throws<DomainRuleException>(() =>
            ProjectFinancialPositionReportSnapshotBuilder.Build(
                Id(501),
                TenantId,
                badZone,
                Cutoff,
                result,
                Cutoff.AddMinutes(1),
                Cutoff.AddMinutes(2)));

        Assert.Equal("reporting.project_financial_position.project_scope.invalid", future.Code);
        Assert.Equal("reporting.project_financial_position.time_zone.invalid", timeZone.Code);
    }

    [Fact]
    public void SourceManifestTamperingFailsClosedAtSnapshotBoundary()
    {
        var result = Calculate(Projection(records: [Record(10, FinancialRecordType.Receipt, 100m)]));
        var exception = Assert.Throws<DomainRuleException>(() => Build(result with
        {
            SourceManifestSha256 = new string('0', 64)
        }));

        Assert.Equal("reporting.project_financial_position.source_manifest.hash_mismatch", exception.Code);
    }

    [Fact]
    public void SettlementOverAllocationFailsClosed()
    {
        var payment = Record(10, FinancialRecordType.Payment, 200m);
        var obligation = Obligation(20, FinancialObligationType.Payable, 100m);
        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(
            records: [payment],
            obligations: [obligation],
            settlements:
            [
                Settlement(30, obligation.ObligationId, payment.RecordId, 60m),
                Settlement(31, obligation.ObligationId, payment.RecordId, 50m)
            ])));

        Assert.Equal("finance.financial_position_reporting.settlement.overallocated", exception.Code);
    }

    [Fact]
    public void NegativePettyCashBalanceIsPreservedWithCanonicalWarning()
    {
        var result = Calculate(Projection(records:
        [Record(10, FinancialRecordType.PettyCashExpense, 25m)]));

        Assert.Equal(-25m, result.Cash.PettyCashBalance);
        Assert.Contains(ProjectFinancialPositionReasonCode.NegativePettyCashBalance, result.ReasonCodes);
        Assert.Equal(ProjectFinancialPositionDataStatus.Available, result.DataStatus);
    }

    private static ProjectFinancialPositionReportingResult Calculate(
        ProjectFinancialPositionReportingProjection projection) =>
        ProjectFinancialPositionReportingCalculator.Calculate(Select(projection));

    private static ProjectFinancialPositionReportingSelection Select(
        ProjectFinancialPositionReportingProjection projection) =>
        ProjectFinancialPositionReportingSelector.Select(projection);

    private static ProjectFinancialPositionReportingProjection Projection(
        IReadOnlyCollection<ProjectFinancialRecordVersion>? records = null,
        IReadOnlyCollection<ProjectFinancialObligationVersion>? obligations = null,
        IReadOnlyCollection<ProjectFinancialSettlementVersion>? settlements = null,
        IReadOnlyCollection<ProjectBudgetBaselineVersion>? budgets = null,
        ProjectFeatureState financeState = ProjectFeatureState.Active,
        ProjectFeatureState budgetState = ProjectFeatureState.Active,
        ProjectFinancialSourceCompleteness ledger = ProjectFinancialSourceCompleteness.Complete,
        ProjectFinancialSourceCompleteness obligationCompleteness = ProjectFinancialSourceCompleteness.Complete,
        ProjectFinancialSourceCompleteness settlementLineage = ProjectFinancialSourceCompleteness.Complete,
        ProjectFinancialPositionReportingClassification sourceClassification =
            ProjectFinancialPositionReportingClassification.Confidential) => new(
        ProjectFinancialPositionReportingContract.Version,
        TenantId,
        ProjectId,
        CutoffLocalDate,
        Cutoff,
        [new ProjectFinancialConfigurationVersion(
            7,
            12,
            financeState,
            budgetState,
            "IRR",
            Cutoff.AddDays(-100),
            null,
            ProjectFinancialPositionReportingClassification.Confidential)],
        records ?? [],
        obligations ?? [],
        settlements ?? [],
        budgets ?? [],
        ledger,
        obligationCompleteness,
        settlementLineage,
        sourceClassification);

    private static ProjectFinancialRecordVersion Record(
        int id,
        FinancialRecordType type,
        decimal amount,
        FinancialRecordStatus status = FinancialRecordStatus.Posted,
        DateOnly? transactionDate = null,
        DateTimeOffset? postedAt = null,
        string currency = "IRR") => new(
        Id(id),
        TenantId,
        ProjectId,
        3,
        type,
        status,
        transactionDate ?? CutoffLocalDate.AddDays(-2),
        amount,
        currency,
        Cutoff.AddDays(-10),
        status == FinancialRecordStatus.Posted ? postedAt ?? Cutoff.AddDays(-2) : null,
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ProjectFinancialObligationVersion Obligation(
        int id,
        FinancialObligationType type,
        decimal amount,
        DateOnly? dueDate = null,
        string? number = null,
        string? counterparty = null,
        FinancialObligationStatus status = FinancialObligationStatus.Approved,
        DateTimeOffset? approvedAt = null,
        string currency = "IRR") => new(
        Id(id),
        TenantId,
        ProjectId,
        4,
        type,
        status,
        number ?? $"OB-{id:000}",
        counterparty,
        CutoffLocalDate.AddDays(-30),
        dueDate ?? CutoffLocalDate.AddDays(10),
        amount,
        currency,
        Cutoff.AddDays(-40),
        status is FinancialObligationStatus.Approved or
            FinancialObligationStatus.PartiallySettled or FinancialObligationStatus.Settled
            ? approvedAt ?? Cutoff.AddDays(-20)
            : null,
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ProjectFinancialSettlementVersion Settlement(
        int id,
        Guid obligationId,
        Guid recordId,
        decimal amount,
        DateTimeOffset? settledAt = null) => new(
        Id(id),
        TenantId,
        ProjectId,
        obligationId,
        recordId,
        amount,
        settledAt ?? Cutoff.AddDays(-1),
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ProjectBudgetBaselineVersion Budget(
        int id,
        decimal amount,
        DateTimeOffset approvedAt,
        DateTimeOffset? supersededAt = null) => new(
        Id(id),
        TenantId,
        ProjectId,
        3,
        supersededAt.HasValue ? BudgetBaselineStatus.Superseded : BudgetBaselineStatus.Approved,
        amount,
        "IRR",
        approvedAt.AddDays(-2),
        approvedAt,
        supersededAt,
        ProjectFinancialPositionReportingClassification.Confidential);

    private static ReportSnapshot Build(
        ProjectFinancialPositionReportingResult source,
        ProjectFinancialPositionPinnedProjectProfile? pinned = null,
        Guid? runId = null,
        DateTimeOffset? builtAt = null) => ProjectFinancialPositionReportSnapshotBuilder.Build(
        runId ?? Id(500),
        TenantId,
        pinned ?? ProjectFinancialPositionPinnedProjectProfile.Capture(
            Profile(),
            Cutoff.AddMinutes(1)),
        Cutoff,
        source,
        Cutoff.AddMinutes(1),
        builtAt ?? Cutoff.AddMinutes(2));

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-01",
        "Project One",
        "Asia/Tehran",
        "IRR",
        12,
        Cutoff.AddDays(-100),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.SimpleWorkList,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 62),
        ConfigurationVersion: 7);

    private static Guid Id(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}
