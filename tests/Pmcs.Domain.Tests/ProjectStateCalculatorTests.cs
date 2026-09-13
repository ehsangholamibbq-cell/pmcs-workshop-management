using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class ProjectStateCalculatorTests
{
    private static readonly DateOnly AsOfDate = new(2026, 9, 9);
    private static readonly DateTimeOffset CalculatedAt = new(2026, 9, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NoApprovedReportProducesNoDataInsteadOfHealthyOrZeroHealth()
    {
        var calculation = Calculate(new ApprovedDailyFactSet(null, null, []));

        Assert.Equal(ProjectOperationalStatus.NoData, calculation.OperationalStatus);
        Assert.Equal(DataCoverageStatus.NoData, calculation.CoverageStatus);
        Assert.Equal(DataFreshnessStatus.NoData, calculation.FreshnessStatus);
        Assert.Equal(DataConfidenceStatus.NoData, calculation.ConfidenceStatus);
        Assert.Equal(ProjectCoverageBasis.FallbackSevenCalendarDays, calculation.CoverageBasis);
        Assert.Equal(0m, calculation.CoveragePercent);
    }

    [Fact]
    public void FourOfSevenApprovedDaysAreInsufficientCoverage()
    {
        var reports = Enumerable.Range(0, 4).Select(day => Report(AsOfDate.AddDays(-day))).ToArray();

        var calculation = Calculate(Source(reports));

        Assert.Equal(57.1m, calculation.CoveragePercent);
        Assert.Equal(DataCoverageStatus.Insufficient, calculation.CoverageStatus);
        Assert.Equal(ProjectOperationalStatus.InsufficientData, calculation.OperationalStatus);
    }

    [Fact]
    public void OperationalAssessmentWorksWithoutWbsBudgetBaselineOrHse()
    {
        var reports = Enumerable.Range(0, 5).Select(day => Report(AsOfDate.AddDays(-day))).ToArray();

        var calculation = Calculate(Source(reports));

        Assert.Equal(ProjectOperationalStatus.Stable, calculation.OperationalStatus);
        Assert.Equal(DataCoverageStatus.Sufficient, calculation.CoverageStatus);
        Assert.True(calculation.IsPartial);
        Assert.Equal(ProjectFeatureState.NotConfigured, calculation.PlanningState);
        Assert.Equal(ProjectFeatureState.SetupRequired, calculation.BudgetState);
        Assert.Equal(ProjectFeatureState.NotEnabled, calculation.HseState);
    }

    [Fact]
    public void CriticalObservedIssueMakesCoveredOperationalStateCritical()
    {
        var reports = Enumerable.Range(0, 5)
            .Select(day => Report(
                AsOfDate.AddDays(-day),
                day == 0 ? Fact(ApprovedDailyFactKind.Issue, ApprovedDailyImpactLevel.Critical) : null))
            .ToArray();

        var calculation = Calculate(Source(reports));

        Assert.Equal(ProjectOperationalStatus.Critical, calculation.OperationalStatus);
        Assert.Equal(1, calculation.CriticalImpactCount);
        Assert.Equal(ProjectAttentionPriority.Critical, Assert.Single(calculation.AttentionItems).Priority);
    }

    [Fact]
    public void AttentionItemPreservesTheStableProjectLocationIdentity()
    {
        var locationId = Guid.NewGuid();
        var reports = Enumerable.Range(0, 5)
            .Select(day => Report(
                AsOfDate.AddDays(-day),
                day == 0 ? Fact(ApprovedDailyFactKind.Issue, ApprovedDailyImpactLevel.High, locationId) : null))
            .ToArray();

        var attention = Assert.Single(Calculate(Source(reports)).AttentionItems);

        Assert.Equal(locationId, attention.LocationId);
        Assert.Equal("Level 3", attention.LocationName);
    }

    [Fact]
    public void MissingImpactRemainsUnassessedAndIsNeverConvertedToLow()
    {
        var reports = Enumerable.Range(0, 5)
            .Select(day => Report(
                AsOfDate.AddDays(-day),
                day == 0 ? Fact(ApprovedDailyFactKind.Issue, null) : null))
            .ToArray();

        var calculation = Calculate(Source(reports));
        var attention = Assert.Single(calculation.AttentionItems);

        Assert.Equal(ProjectOperationalStatus.Watch, calculation.OperationalStatus);
        Assert.Equal(ProjectAttentionPriority.Unassessed, attention.Priority);
        Assert.Null(attention.ObservedImpact);
    }

    [Fact]
    public void HistoricalApprovedDataCanBeStaleWithoutBeingTreatedAsNoHistory()
    {
        var lastDate = AsOfDate.AddDays(-10);
        var source = new ApprovedDailyFactSet(lastDate, CalculatedAt.AddDays(-10), []);

        var calculation = Calculate(source);

        Assert.Equal(DataFreshnessStatus.Stale, calculation.FreshnessStatus);
        Assert.Equal(DataConfidenceStatus.Low, calculation.ConfidenceStatus);
        Assert.Equal(ProjectOperationalStatus.InsufficientData, calculation.OperationalStatus);
    }

    [Fact]
    public void UnassessedStoppageIsAtRiskAndKeepsItsIndependentAgingBand()
    {
        var recentReports = Enumerable.Range(0, 5).Select(day => Report(AsOfDate.AddDays(-day)));
        var stoppageReport = Report(
            AsOfDate.AddDays(-7),
            Fact(ApprovedDailyFactKind.Stoppage, null));

        var calculation = Calculate(Source([.. recentReports, stoppageReport]));
        var attention = Assert.Single(calculation.AttentionItems);

        Assert.Equal(ProjectOperationalStatus.AtRisk, calculation.OperationalStatus);
        Assert.Equal(ProjectAttentionPriority.Unassessed, attention.Priority);
        Assert.Equal(ProjectAttentionAgeBand.Overdue, attention.AgeBand);
        Assert.Equal(7, attention.AgeDays);
    }

    [Fact]
    public void ConfiguredWorkingWeekChangesCoverageDenominatorWithoutRequiringWbs()
    {
        var reports = Enumerable.Range(0, 5).Select(day => Report(AsOfDate.AddDays(-day))).ToArray();
        var workingDays = (1 << (int)DayOfWeek.Saturday) |
            (1 << (int)DayOfWeek.Sunday) |
            (1 << (int)DayOfWeek.Monday) |
            (1 << (int)DayOfWeek.Tuesday) |
            (1 << (int)DayOfWeek.Wednesday);

        var calculation = ProjectStateCalculator.Calculate(
            Profile(new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, workingDays)),
            Source(reports),
            AsOfDate,
            CalculatedAt);

        Assert.Equal(ProjectCoverageBasis.ConfiguredWorkingDays, calculation.CoverageBasis);
        Assert.Equal(5, calculation.ExpectedReportDays);
        Assert.Equal(100m, calculation.CoveragePercent);
        Assert.Equal(2, calculation.ProjectConfigurationRevision);
    }

    private static ProjectStateCalculation Calculate(ApprovedDailyFactSet source) =>
        ProjectStateCalculator.Calculate(Profile(), source, AsOfDate, CalculatedAt);

    private static ApprovedDailyFactSet Source(IReadOnlyCollection<ApprovedDailyReportRecord> reports) => new(
        reports.Max(report => report.ReportDate),
        reports.Max(report => report.ApprovedAt),
        reports);

    private static ApprovedDailyReportRecord Report(
        DateOnly date,
        ApprovedDailyFactRecord? specialFact = null) => new(
        Guid.NewGuid(),
        date,
        CalculatedAt.AddDays(date.DayNumber - AsOfDate.DayNumber),
        specialFact is null
            ? [Fact(ApprovedDailyFactKind.Note, null)]
            : [specialFact]);

    private static ApprovedDailyFactRecord Fact(
        ApprovedDailyFactKind kind,
        ApprovedDailyImpactLevel? impactLevel,
        Guid? locationId = null) => new(
        Guid.NewGuid(),
        kind,
        kind == ApprovedDailyFactKind.Issue ? "Approved drawing was unavailable." : "Observed fact",
        null,
        "Level 3",
        null,
        null,
        null,
        null,
        impactLevel,
        null,
        null,
        locationId);

    private static ProjectControlProfile Profile(ProjectCalendarProfile? calendar = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "DEMO-01",
        "Demo project",
        "Asia/Tehran",
        "IRR",
        2,
        CalculatedAt.AddMinutes(-1),
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
        calendar ?? new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null));
}
