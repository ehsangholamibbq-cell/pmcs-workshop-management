using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Domain;

public static class ProjectStateCalculator
{
    public const string CalculationVersion = "project-state-v2";
    public const int CoverageWindowDays = 7;
    public const int AttentionWindowDays = 30;
    public const decimal MinimumCoveragePercent = 60m;

    public static ProjectStateCalculation Calculate(
        ProjectControlProfile project,
        ApprovedDailyFactSet source,
        DateOnly asOfDate,
        DateTimeOffset calculatedAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);

        var coverageStart = asOfDate.AddDays(-(CoverageWindowDays - 1));
        var sourceStart = asOfDate.AddDays(-(AttentionWindowDays - 1));
        var expectedDates = Enumerable.Range(0, CoverageWindowDays)
            .Select(day => coverageStart.AddDays(day))
            .Where(date => project.Calendar.State != ProjectCalendarConfigurationState.Configured ||
                project.Calendar.IsWorkingDay(date.DayOfWeek))
            .ToHashSet();
        var expectedReportDays = expectedDates.Count;
        if (expectedReportDays == 0)
        {
            throw new InvalidOperationException("A configured project calendar must include at least one working day.");
        }
        var reports = source.Reports
            .Where(report => report.ReportDate >= sourceStart && report.ReportDate <= asOfDate)
            .ToArray();
        var coverageReports = reports
            .Where(report => report.ReportDate >= coverageStart)
            .ToArray();
        var approvedReportDays = coverageReports
            .Where(report => expectedDates.Contains(report.ReportDate))
            .Select(report => report.ReportDate)
            .Distinct()
            .Count();
        var coveragePercent = Math.Round(
            approvedReportDays * 100m / expectedReportDays,
            1,
            MidpointRounding.AwayFromZero);
        var coverageStatus = approvedReportDays switch
        {
            0 => DataCoverageStatus.NoData,
            _ when coveragePercent < MinimumCoveragePercent => DataCoverageStatus.Insufficient,
            _ => DataCoverageStatus.Sufficient
        };

        var freshnessStatus = CalculateFreshness(source.LastApprovedReportDate, asOfDate);
        var confidenceStatus = source.LastApprovedReportDate switch
        {
            null => DataConfidenceStatus.NoData,
            _ when coverageStatus != DataCoverageStatus.Sufficient || freshnessStatus == DataFreshnessStatus.Stale =>
                DataConfidenceStatus.Low,
            _ => DataConfidenceStatus.Adequate
        };
        var attentionItems = BuildAttentionItems(reports, asOfDate);
        var operationalStatus = CalculateOperationalStatus(
            source.LastApprovedReportDate,
            coverageStatus,
            freshnessStatus,
            attentionItems);
        var facts = coverageReports.SelectMany(report => report.Facts).ToArray();

        return new ProjectStateCalculation(
            project.TenantId,
            project.Id,
            project.Code,
            project.Name,
            CalculationVersion,
            project.Revision,
            asOfDate,
            coverageStart,
            asOfDate,
            calculatedAt,
            ProjectAssessmentScope.ApprovedDailyOperations,
            IsPartial: true,
            operationalStatus,
            coverageStatus,
            freshnessStatus,
            confidenceStatus,
            project.Calendar.State == ProjectCalendarConfigurationState.Configured
                ? ProjectCoverageBasis.ConfiguredWorkingDays
                : ProjectCoverageBasis.FallbackSevenCalendarDays,
            coveragePercent,
            expectedReportDays,
            approvedReportDays,
            source.LastApprovedReportDate,
            facts.Length,
            facts.Count(fact => fact.Kind == ApprovedDailyFactKind.WorkProgress),
            facts.Count(fact => fact.Kind == ApprovedDailyFactKind.Labor),
            facts.Count(fact => fact.Kind == ApprovedDailyFactKind.Equipment),
            facts.Count(fact => fact.Kind == ApprovedDailyFactKind.Material),
            attentionItems.Count(item => item.Kind == ProjectAttentionKind.Issue),
            attentionItems.Count(item => item.Kind == ProjectAttentionKind.Stoppage),
            attentionItems.Count(item => item.Priority == ProjectAttentionPriority.High),
            attentionItems.Count(item => item.Priority == ProjectAttentionPriority.Critical),
            attentionItems.Length == 0 ? null : attentionItems.Max(item => item.AgeDays),
            project.Contract,
            project.Planning,
            project.Budget,
            project.Quality,
            project.Hse,
            source.LatestApprovedChangeAt,
            attentionItems);
    }

    private static DataFreshnessStatus CalculateFreshness(DateOnly? lastApprovedDate, DateOnly asOfDate)
    {
        if (!lastApprovedDate.HasValue)
        {
            return DataFreshnessStatus.NoData;
        }

        var ageDays = Math.Max(0, asOfDate.DayNumber - lastApprovedDate.Value.DayNumber);
        return ageDays switch
        {
            <= 1 => DataFreshnessStatus.Current,
            <= 3 => DataFreshnessStatus.Aging,
            _ => DataFreshnessStatus.Stale
        };
    }

    private static ProjectAttentionCalculation[] BuildAttentionItems(
        IEnumerable<ApprovedDailyReportRecord> reports,
        DateOnly asOfDate) =>
        reports
            .SelectMany(report => report.Facts
                .Where(fact => fact.Kind is ApprovedDailyFactKind.Issue or ApprovedDailyFactKind.Stoppage)
                .Select(fact => BuildAttentionItem(report, fact, asOfDate)))
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => item.AgeDays)
            .ThenBy(item => item.ReportDate)
            .ToArray();

    private static ProjectAttentionCalculation BuildAttentionItem(
        ApprovedDailyReportRecord report,
        ApprovedDailyFactRecord fact,
        DateOnly asOfDate)
    {
        var ageDays = Math.Max(0, asOfDate.DayNumber - report.ReportDate.DayNumber);
        return new ProjectAttentionCalculation(
            report.ReportId,
            fact.FactId,
            report.ReportDate,
            fact.Kind == ApprovedDailyFactKind.Stoppage
                ? ProjectAttentionKind.Stoppage
                : ProjectAttentionKind.Issue,
            fact.Description,
            fact.Category,
            fact.LocationName,
            fact.ImpactLevel switch
            {
                ApprovedDailyImpactLevel.Low => ProjectObservedImpact.Low,
                ApprovedDailyImpactLevel.Medium => ProjectObservedImpact.Medium,
                ApprovedDailyImpactLevel.High => ProjectObservedImpact.High,
                ApprovedDailyImpactLevel.Critical => ProjectObservedImpact.Critical,
                null => null,
                _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.ImpactLevel, "Unsupported observed impact.")
            },
            fact.ImpactLevel switch
            {
                ApprovedDailyImpactLevel.Low => ProjectAttentionPriority.Low,
                ApprovedDailyImpactLevel.Medium => ProjectAttentionPriority.Medium,
                ApprovedDailyImpactLevel.High => ProjectAttentionPriority.High,
                ApprovedDailyImpactLevel.Critical => ProjectAttentionPriority.Critical,
                null => ProjectAttentionPriority.Unassessed,
                _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.ImpactLevel, "Unsupported attention priority.")
            },
            ageDays,
            ageDays switch
            {
                <= 1 => ProjectAttentionAgeBand.New,
                <= 6 => ProjectAttentionAgeBand.Aging,
                _ => ProjectAttentionAgeBand.Overdue
            },
            ProjectAttentionStatus.NeedsTriage,
            fact.ReferenceCode,
            fact.LocationId);
    }

    private static ProjectOperationalStatus CalculateOperationalStatus(
        DateOnly? lastApprovedReportDate,
        DataCoverageStatus coverageStatus,
        DataFreshnessStatus freshnessStatus,
        ProjectAttentionCalculation[] attentionItems)
    {
        if (!lastApprovedReportDate.HasValue)
        {
            return ProjectOperationalStatus.NoData;
        }

        if (coverageStatus != DataCoverageStatus.Sufficient || freshnessStatus == DataFreshnessStatus.Stale)
        {
            return ProjectOperationalStatus.InsufficientData;
        }

        if (attentionItems.Any(item => item.Priority == ProjectAttentionPriority.Critical))
        {
            return ProjectOperationalStatus.Critical;
        }

        if (attentionItems.Any(item => item.Priority == ProjectAttentionPriority.High ||
                item.Kind == ProjectAttentionKind.Stoppage))
        {
            return ProjectOperationalStatus.AtRisk;
        }

        if (attentionItems.Length > 0 || freshnessStatus == DataFreshnessStatus.Aging)
        {
            return ProjectOperationalStatus.Watch;
        }

        return ProjectOperationalStatus.Stable;
    }

    private static int PriorityRank(ProjectAttentionPriority priority) => priority switch
    {
        ProjectAttentionPriority.Critical => 5,
        ProjectAttentionPriority.High => 4,
        ProjectAttentionPriority.Medium => 3,
        ProjectAttentionPriority.Low => 2,
        ProjectAttentionPriority.Unassessed => 1,
        _ => 0
    };
}

public sealed record ProjectStateCalculation(
    Guid TenantId,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string CalculationVersion,
    long ProjectConfigurationRevision,
    DateOnly AsOfDate,
    DateOnly WindowStart,
    DateOnly WindowEnd,
    DateTimeOffset CalculatedAt,
    ProjectAssessmentScope AssessmentScope,
    bool IsPartial,
    ProjectOperationalStatus OperationalStatus,
    DataCoverageStatus CoverageStatus,
    DataFreshnessStatus FreshnessStatus,
    DataConfidenceStatus ConfidenceStatus,
    ProjectCoverageBasis CoverageBasis,
    decimal CoveragePercent,
    int ExpectedReportDays,
    int ApprovedReportDays,
    DateOnly? LastApprovedReportDate,
    int ApprovedFactCount,
    int ProgressFactCount,
    int LaborFactCount,
    int EquipmentFactCount,
    int MaterialFactCount,
    int IssueCount,
    int StoppageCount,
    int HighImpactCount,
    int CriticalImpactCount,
    int? OldestAttentionAgeDays,
    ProjectFeatureState ContractState,
    ProjectFeatureState PlanningState,
    ProjectFeatureState BudgetState,
    ProjectFeatureState QualityState,
    ProjectFeatureState HseState,
    DateTimeOffset? SourceMaxChangedAt,
    IReadOnlyCollection<ProjectAttentionCalculation> AttentionItems);

public sealed record ProjectAttentionCalculation(
    Guid SourceReportId,
    Guid SourceFactId,
    DateOnly ReportDate,
    ProjectAttentionKind Kind,
    string Description,
    string? Category,
    string? LocationName,
    ProjectObservedImpact? ObservedImpact,
    ProjectAttentionPriority Priority,
    int AgeDays,
    ProjectAttentionAgeBand AgeBand,
    ProjectAttentionStatus Status,
    string? ReferenceCode,
    Guid? LocationId = null);

public enum ProjectAssessmentScope
{
    ApprovedDailyOperations = 1
}

public enum ProjectOperationalStatus
{
    NoData = 1,
    InsufficientData = 2,
    Stable = 3,
    Watch = 4,
    AtRisk = 5,
    Critical = 6
}

public enum DataCoverageStatus
{
    NoData = 1,
    Insufficient = 2,
    Sufficient = 3
}

public enum DataFreshnessStatus
{
    NoData = 1,
    Current = 2,
    Aging = 3,
    Stale = 4
}

public enum DataConfidenceStatus
{
    NoData = 1,
    Low = 2,
    Adequate = 3
}

public enum ProjectCoverageBasis
{
    SevenCalendarDays = 1,
    FallbackSevenCalendarDays = 2,
    ConfiguredWorkingDays = 3
}

public enum ProjectAttentionKind
{
    Issue = 1,
    Stoppage = 2
}

public enum ProjectObservedImpact
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ProjectAttentionPriority
{
    Unassessed = 1,
    Low = 2,
    Medium = 3,
    High = 4,
    Critical = 5
}

public enum ProjectAttentionAgeBand
{
    New = 1,
    Aging = 2,
    Overdue = 3
}

public enum ProjectAttentionStatus
{
    NeedsTriage = 1
}
