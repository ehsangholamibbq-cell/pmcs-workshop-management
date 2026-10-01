using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Services;

internal static class ProjectFinancialPositionReportingCalculator
{
    public static ProjectFinancialPositionReportingResult Calculate(
        ProjectFinancialPositionReportingSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ValidateSelection(selection);

        if (selection.Configuration is null)
        {
            return NotConfigured(
                selection,
                ProjectFinancialPositionReasonCode.FinanceReportingNotConfigured);
        }

        var financeReason = selection.Configuration.FinanceState switch
        {
            ProjectFeatureState.NotConfigured or ProjectFeatureState.NotEnabled =>
                ProjectFinancialPositionReasonCode.FinanceReportingNotConfigured,
            ProjectFeatureState.SetupRequired => ProjectFinancialPositionReasonCode.FinanceSetupRequired,
            ProjectFeatureState.Suspended => ProjectFinancialPositionReasonCode.FinanceSuspended,
            ProjectFeatureState.Active => (ProjectFinancialPositionReasonCode?)null,
            _ => throw Invalid("configuration.finance_state.invalid", "Finance state is unknown.")
        };
        if (financeReason.HasValue)
        {
            return NotConfigured(selection, financeReason.Value);
        }

        var reasons = new HashSet<ProjectFinancialPositionReasonCode>();
        var cash = CalculateCash(selection, reasons);
        var obligations = CalculateObligations(selection, reasons);
        var budget = CalculateBudget(selection, cash.Status, cash.Summary, reasons);
        var dataStatus = cash.Status == ProjectFinancialPositionSectionStatus.NoData &&
            obligations.Status == ProjectFinancialPositionSectionStatus.NoData
                ? ProjectFinancialPositionDataStatus.NoData
                : cash.Status == ProjectFinancialPositionSectionStatus.InsufficientData ||
                    obligations.Status == ProjectFinancialPositionSectionStatus.InsufficientData
                    ? ProjectFinancialPositionDataStatus.InsufficientData
                    : ProjectFinancialPositionDataStatus.Available;
        var counts = selection.SourceCounts with
        {
            OpenObligationCount = obligations.Rows.Length
        };

        return new ProjectFinancialPositionReportingResult(
            ProjectFinancialPositionReportingContract.Version,
            ProjectFinancialPositionReportingContract.PolicyVersion,
            selection.TenantId,
            selection.ProjectId,
            selection.CutoffLocalDate,
            selection.SourceCutoffUtc,
            selection.Classification,
            dataStatus,
            reasons.OrderBy(item => item).ToArray(),
            selection.Configuration,
            cash.Status,
            cash.Summary,
            obligations.Status,
            budget.Status,
            budget.ComparisonStatus,
            budget.Identity,
            budget.Comparison,
            obligations.Payable,
            obligations.Receivable,
            obligations.Aging,
            obligations.Rows,
            counts,
            selection.SourceMaxChangedAt,
            selection.SourceManifest,
            selection.SourceManifestSha256);
    }

    private static CashCalculation CalculateCash(
        ProjectFinancialPositionReportingSelection selection,
        HashSet<ProjectFinancialPositionReasonCode> reasons)
    {
        if (selection.LedgerCompleteness == ProjectFinancialSourceCompleteness.Incomplete)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.FinancialSourceIncomplete);
            return new CashCalculation(
                ProjectFinancialPositionSectionStatus.InsufficientData,
                EmptyCash());
        }
        if (selection.OfficialFinancialRecords.Count == 0)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.OfficialFinancialRecordsMissing);
            return new CashCalculation(ProjectFinancialPositionSectionStatus.NoData, EmptyCash());
        }

        var receipts = Sum(selection.OfficialFinancialRecords, FinancialRecordType.Receipt);
        var payments = Sum(selection.OfficialFinancialRecords, FinancialRecordType.Payment);
        var funding = Sum(selection.OfficialFinancialRecords, FinancialRecordType.PettyCashFunding);
        var expenses = Sum(selection.OfficialFinancialRecords, FinancialRecordType.PettyCashExpense);
        var pettyCashBalance = funding - expenses;
        if (pettyCashBalance < 0)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.NegativePettyCashBalance);
        }

        return new CashCalculation(
            ProjectFinancialPositionSectionStatus.Available,
            new ProjectFinancialPositionCashSummary(
                receipts,
                payments,
                funding,
                expenses,
                receipts - payments - funding,
                payments + expenses,
                pettyCashBalance));
    }

    private static ObligationCalculation CalculateObligations(
        ProjectFinancialPositionReportingSelection selection,
        HashSet<ProjectFinancialPositionReasonCode> reasons)
    {
        if (selection.ObligationCompleteness == ProjectFinancialSourceCompleteness.Incomplete ||
            selection.SettlementLineageCompleteness == ProjectFinancialSourceCompleteness.Incomplete)
        {
            if (selection.ObligationCompleteness == ProjectFinancialSourceCompleteness.Incomplete)
            {
                reasons.Add(ProjectFinancialPositionReasonCode.FinancialSourceIncomplete);
            }
            if (selection.SettlementLineageCompleteness == ProjectFinancialSourceCompleteness.Incomplete)
            {
                reasons.Add(ProjectFinancialPositionReasonCode.ObligationSettlementLineageIncomplete);
            }

            return new ObligationCalculation(
                ProjectFinancialPositionSectionStatus.InsufficientData,
                null,
                null,
                [],
                []);
        }
        if (selection.OfficialObligations.Count == 0)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.OfficialObligationsMissing);
            return new ObligationCalculation(
                ProjectFinancialPositionSectionStatus.NoData,
                null,
                null,
                [],
                []);
        }

        var settledByObligation = selection.EligibleSettlements
            .GroupBy(item => item.ObligationId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
        var rows = selection.OfficialObligations
            .Select(item =>
            {
                var settled = settledByObligation.GetValueOrDefault(item.ObligationId);
                var outstanding = item.Amount - settled;
                if (outstanding < 0)
                {
                    throw Invalid(
                        "settlement.overallocated",
                        "Settlement lineage exceeds the official obligation amount.");
                }

                return outstanding == 0
                    ? null
                    : new ProjectFinancialPositionOpenObligation(
                        item.ObligationId,
                        item.Type,
                        item.NumberSnapshot,
                        item.CounterpartySnapshot,
                        item.IssueDate,
                        item.DueDate,
                        item.Amount,
                        settled,
                        outstanding,
                        Bucket(item.DueDate, selection.CutoffLocalDate));
            })
            .Where(item => item is not null)
            .Cast<ProjectFinancialPositionOpenObligation>()
            .OrderBy(item => item.Type)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.NumberSnapshot, StringComparer.Ordinal)
            .ThenBy(item => item.ObligationId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        var aging = Enum.GetValues<FinancialObligationType>()
            .SelectMany(type => Enum.GetValues<ProjectFinancialPositionAgingBucket>()
                .Select(bucket =>
                {
                    var matching = rows.Where(item => item.Type == type && item.Bucket == bucket).ToArray();
                    return new ProjectFinancialPositionAgingSummary(
                        type,
                        bucket,
                        matching.Length,
                        matching.Sum(item => item.OutstandingAmount));
                }))
            .ToArray();

        return new ObligationCalculation(
            ProjectFinancialPositionSectionStatus.Available,
            Summary(FinancialObligationType.Payable, rows),
            Summary(FinancialObligationType.Receivable, rows),
            aging,
            rows);
    }

    private static BudgetCalculation CalculateBudget(
        ProjectFinancialPositionReportingSelection selection,
        ProjectFinancialPositionSectionStatus cashStatus,
        ProjectFinancialPositionCashSummary cash,
        HashSet<ProjectFinancialPositionReasonCode> reasons)
    {
        var state = selection.Configuration!.BudgetState;
        if (state is ProjectFeatureState.NotConfigured or ProjectFeatureState.NotEnabled)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.BudgetNotConfigured);
            return new BudgetCalculation(
                ProjectFinancialPositionSectionStatus.NotConfigured,
                ProjectFinancialPositionSectionStatus.NotConfigured,
                null,
                null);
        }
        if (state == ProjectFeatureState.SetupRequired)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.BudgetSetupRequired);
            return new BudgetCalculation(
                ProjectFinancialPositionSectionStatus.SetupRequired,
                ProjectFinancialPositionSectionStatus.SetupRequired,
                null,
                null);
        }
        if (state == ProjectFeatureState.Suspended)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.BudgetSuspended);
            return new BudgetCalculation(
                ProjectFinancialPositionSectionStatus.Suspended,
                ProjectFinancialPositionSectionStatus.Suspended,
                null,
                null);
        }
        if (state != ProjectFeatureState.Active)
        {
            throw Invalid("configuration.budget_state.invalid", "Budget state is unknown.");
        }
        if (selection.BudgetBaseline is null)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.OfficialBudgetBaselineMissing);
            return new BudgetCalculation(
                ProjectFinancialPositionSectionStatus.NoData,
                ProjectFinancialPositionSectionStatus.NoData,
                null,
                null);
        }

        var baseline = selection.BudgetBaseline;
        var identity = new ProjectFinancialPositionBudgetIdentity(
            baseline.BaselineId,
            baseline.Revision,
            baseline.Amount,
            baseline.CurrencyCode,
            baseline.ApprovedAt!.Value,
            baseline.SupersededAt);
        if (cashStatus != ProjectFinancialPositionSectionStatus.Available)
        {
            reasons.Add(ProjectFinancialPositionReasonCode.OfficialCashDataMissingForBudgetComparison);
            return new BudgetCalculation(
                ProjectFinancialPositionSectionStatus.Available,
                cashStatus == ProjectFinancialPositionSectionStatus.InsufficientData
                    ? ProjectFinancialPositionSectionStatus.InsufficientData
                    : ProjectFinancialPositionSectionStatus.NoData,
                identity,
                new ProjectFinancialPositionBudgetComparison(baseline.Amount, null, null));
        }

        var recognizedSpend = cash.RecognizedSpend!.Value;
        return new BudgetCalculation(
            ProjectFinancialPositionSectionStatus.Available,
            ProjectFinancialPositionSectionStatus.Available,
            identity,
            new ProjectFinancialPositionBudgetComparison(
                baseline.Amount,
                baseline.Amount - recognizedSpend,
                decimal.Round(
                    recognizedSpend * 100m / baseline.Amount,
                    1,
                    MidpointRounding.AwayFromZero)));
    }

    private static ProjectFinancialPositionReportingResult NotConfigured(
        ProjectFinancialPositionReportingSelection selection,
        ProjectFinancialPositionReasonCode reason) => new(
        ProjectFinancialPositionReportingContract.Version,
        ProjectFinancialPositionReportingContract.PolicyVersion,
        selection.TenantId,
        selection.ProjectId,
        selection.CutoffLocalDate,
        selection.SourceCutoffUtc,
        selection.Classification,
        ProjectFinancialPositionDataStatus.NotConfigured,
        [reason],
        selection.Configuration,
        ProjectFinancialPositionSectionStatus.NotConfigured,
        EmptyCash(),
        ProjectFinancialPositionSectionStatus.NotConfigured,
        ProjectFinancialPositionSectionStatus.NotConfigured,
        ProjectFinancialPositionSectionStatus.NotConfigured,
        null,
        null,
        null,
        null,
        [],
        [],
        selection.SourceCounts,
        selection.SourceMaxChangedAt,
        selection.SourceManifest,
        selection.SourceManifestSha256);

    private static ProjectFinancialPositionObligationSummary Summary(
        FinancialObligationType type,
        ProjectFinancialPositionOpenObligation[] rows)
    {
        var matching = rows.Where(item => item.Type == type).ToArray();
        var overdue = matching.Where(item => item.Bucket != ProjectFinancialPositionAgingBucket.NotDue).ToArray();
        return new ProjectFinancialPositionObligationSummary(
            type,
            matching.Length,
            matching.Sum(item => item.OutstandingAmount),
            overdue.Length,
            overdue.Sum(item => item.OutstandingAmount));
    }

    private static ProjectFinancialPositionAgingBucket Bucket(DateOnly dueDate, DateOnly cutoffLocalDate)
    {
        var days = cutoffLocalDate.DayNumber - dueDate.DayNumber;
        return days <= 0
            ? ProjectFinancialPositionAgingBucket.NotDue
            : days <= 30
                ? ProjectFinancialPositionAgingBucket.Overdue1To30
                : days <= 60
                    ? ProjectFinancialPositionAgingBucket.Overdue31To60
                    : ProjectFinancialPositionAgingBucket.Overdue61Plus;
    }

    private static decimal Sum(
        IReadOnlyCollection<ProjectFinancialRecordVersion> records,
        FinancialRecordType type) => records.Where(item => item.Type == type).Sum(item => item.Amount);

    private static ProjectFinancialPositionCashSummary EmptyCash() =>
        new(null, null, null, null, null, null, null);

    private static void ValidateSelection(ProjectFinancialPositionReportingSelection selection)
    {
        if (!string.Equals(
                selection.ContractVersion,
                ProjectFinancialPositionReportingContract.Version,
                StringComparison.Ordinal) ||
            selection.TenantId == Guid.Empty || selection.ProjectId == Guid.Empty ||
            selection.CutoffLocalDate == default || selection.SourceCutoffUtc == default ||
            !Enum.IsDefined(selection.Classification) ||
            selection.Classification < ProjectFinancialPositionReportingClassification.Confidential ||
            !Enum.IsDefined(selection.LedgerCompleteness) ||
            !Enum.IsDefined(selection.ObligationCompleteness) ||
            !Enum.IsDefined(selection.SettlementLineageCompleteness) ||
            selection.OfficialFinancialRecords is null || selection.OfficialObligations is null ||
            selection.EligibleSettlements is null || selection.SourceCounts is null ||
            selection.SourceManifest is null || string.IsNullOrWhiteSpace(selection.SourceManifestSha256) ||
            selection.SourceMaxChangedAt?.ToUniversalTime() > selection.SourceCutoffUtc.ToUniversalTime() ||
            !string.Equals(
                selection.SourceManifestSha256,
                ProjectFinancialPositionCanonicalJson.Sha256(
                    ProjectFinancialPositionCanonicalJson.Serialize(selection.SourceManifest)),
                StringComparison.Ordinal) ||
            !string.Equals(
                selection.SourceManifest.ManifestVersion,
                ProjectFinancialPositionReportingContract.SourceManifestVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                selection.SourceManifest.SourceContractVersion,
                ProjectFinancialPositionReportingContract.Version,
                StringComparison.Ordinal) ||
            !string.Equals(
                selection.SourceManifest.PolicyVersion,
                ProjectFinancialPositionReportingContract.PolicyVersion,
                StringComparison.Ordinal) ||
            selection.SourceManifest.TenantId != selection.TenantId ||
            selection.SourceManifest.ProjectId != selection.ProjectId ||
            selection.SourceManifest.CutoffLocalDate != selection.CutoffLocalDate ||
            selection.SourceManifest.SourceCutoffUtc.ToUniversalTime() !=
                selection.SourceCutoffUtc.ToUniversalTime())
        {
            throw Invalid(
                "selection.invalid",
                "The selected financial position source violates its versioned contract.");
        }
    }

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"finance.financial_position_reporting.{suffix}", message);

    private sealed record CashCalculation(
        ProjectFinancialPositionSectionStatus Status,
        ProjectFinancialPositionCashSummary Summary);

    private sealed record ObligationCalculation(
        ProjectFinancialPositionSectionStatus Status,
        ProjectFinancialPositionObligationSummary? Payable,
        ProjectFinancialPositionObligationSummary? Receivable,
        ProjectFinancialPositionAgingSummary[] Aging,
        ProjectFinancialPositionOpenObligation[] Rows);

    private sealed record BudgetCalculation(
        ProjectFinancialPositionSectionStatus Status,
        ProjectFinancialPositionSectionStatus ComparisonStatus,
        ProjectFinancialPositionBudgetIdentity? Identity,
        ProjectFinancialPositionBudgetComparison? Comparison);
}
