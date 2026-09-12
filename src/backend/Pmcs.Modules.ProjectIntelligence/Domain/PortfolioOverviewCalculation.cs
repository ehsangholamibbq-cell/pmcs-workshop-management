using Pmcs.Modules.Commercial.Contracts;
using Pmcs.Modules.Commercial.Domain;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.ProjectIntelligence.Domain;

public static class PortfolioOverviewCalculator
{
    public const string ContractVersion = "portfolio-command-center-v1";

    public static PortfolioOverviewCalculation Calculate(
        IReadOnlyCollection<PortfolioProjectOverviewInput> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var exposures = projects
            .SelectMany(ProjectExposures)
            .GroupBy(item => item.CurrencyCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PortfolioCurrencyExposure(
                group.Key.ToUpperInvariant(),
                group.Sum(item => item.FinancialProjectCount),
                group.Sum(item => item.CommercialProjectCount),
                group.Sum(item => item.RecognizedSpend),
                group.Sum(item => item.ExternalNetCash),
                group.Sum(item => item.TotalCommittedAmount),
                group.Sum(item => item.OpenCommitmentAmount)))
            .OrderBy(item => item.CurrencyCode, StringComparer.Ordinal)
            .ToArray();

        return new PortfolioOverviewCalculation(
            ContractVersion,
            projects.Count,
            projects.Count(item => item.LifecycleStatus == ProjectStatus.Active),
            projects.Count(item => item.LifecycleStatus == ProjectStatus.OnHold),
            projects.Count(item => item.LifecycleStatus == ProjectStatus.Closing),
            projects.Count(item => OperationalStatus(item) == ProjectOperationalStatus.Stable),
            projects.Count(item => OperationalStatus(item) == ProjectOperationalStatus.Watch),
            projects.Count(item => OperationalStatus(item) == ProjectOperationalStatus.AtRisk),
            projects.Count(item => OperationalStatus(item) == ProjectOperationalStatus.Critical),
            projects.Count(item => OperationalStatus(item) == ProjectOperationalStatus.InsufficientData),
            projects.Count(item => OperationalStatus(item) == ProjectOperationalStatus.NoData),
            projects.Count(item => item.HasOperationalSnapshot && item.FreshnessStatus == DataFreshnessStatus.Stale),
            projects.Count(item => item.IsOutdated),
            projects.Count(item => item.ActionsVisible),
            projects.Where(item => item.ActionsVisible).Sum(item => item.OpenActionCount),
            projects.Where(item => item.ActionsVisible).Sum(item => item.OverdueActionCount),
            projects.Where(item => item.CommercialState is not null).Sum(item =>
                item.CommercialState!.PendingContractApprovalCount +
                item.CommercialState.PendingProcurementApprovalCount),
            projects.Select(item => item.LatestSnapshotAt).Where(value => value.HasValue).Max(),
            exposures);
    }

    private static ProjectOperationalStatus OperationalStatus(PortfolioProjectOverviewInput project) =>
        project.HasOperationalSnapshot && project.OperationalStatus.HasValue
            ? project.OperationalStatus.Value
            : ProjectOperationalStatus.NoData;

    private static IEnumerable<PortfolioExposureContribution> ProjectExposures(
        PortfolioProjectOverviewInput project)
    {
        if (project.FinancialState?.Status == FinancialStateStatus.Available)
        {
            yield return new PortfolioExposureContribution(
                project.FinancialState.CurrencyCode,
                1,
                0,
                project.FinancialState.RecognizedSpend,
                project.FinancialState.ExternalNetCash,
                0,
                0);
        }

        if (project.CommercialState?.ProcurementState == CommercialMetricState.Available)
        {
            yield return new PortfolioExposureContribution(
                project.CommercialState.CurrencyCode,
                0,
                1,
                0,
                0,
                project.CommercialState.TotalCommittedAmount,
                project.CommercialState.OpenCommitmentAmount);
        }
    }
}

public sealed record PortfolioProjectOverviewInput(
    Guid ProjectId,
    ProjectStatus LifecycleStatus,
    bool HasOperationalSnapshot,
    ProjectOperationalStatus? OperationalStatus,
    DataFreshnessStatus? FreshnessStatus,
    bool IsOutdated,
    bool ActionsVisible,
    int OpenActionCount,
    int OverdueActionCount,
    FinancialStateRecord? FinancialState,
    CommercialStateRecord? CommercialState,
    DateTimeOffset? LatestSnapshotAt);

public sealed record PortfolioOverviewCalculation(
    string ContractVersion,
    int ProjectCount,
    int ActiveProjectCount,
    int OnHoldProjectCount,
    int ClosingProjectCount,
    int StableProjectCount,
    int WatchProjectCount,
    int AtRiskProjectCount,
    int CriticalProjectCount,
    int InsufficientDataProjectCount,
    int NoDataProjectCount,
    int StaleProjectCount,
    int OutdatedProjectCount,
    int ActionVisibleProjectCount,
    int OpenActionCount,
    int OverdueActionCount,
    int PendingCommercialApprovalCount,
    DateTimeOffset? LatestSnapshotAt,
    IReadOnlyCollection<PortfolioCurrencyExposure> CurrencyExposures);

public sealed record PortfolioCurrencyExposure(
    string CurrencyCode,
    int FinancialProjectCount,
    int CommercialProjectCount,
    decimal RecognizedSpend,
    decimal ExternalNetCash,
    decimal TotalCommittedAmount,
    decimal OpenCommitmentAmount);

internal sealed record PortfolioExposureContribution(
    string CurrencyCode,
    int FinancialProjectCount,
    int CommercialProjectCount,
    decimal RecognizedSpend,
    decimal ExternalNetCash,
    decimal TotalCommittedAmount,
    decimal OpenCommitmentAmount);
