using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Contracts;

public static class ProjectStateReportingContract
{
    public const string Version = "pmcs.project-intelligence.project-state-reporting/v1";
    public const int MaximumTrendDates = 14;

    public static bool SupportsCalculationVersion(string value) =>
        string.Equals(value, "project-state-v1", StringComparison.Ordinal) ||
        string.Equals(value, ProjectStateCalculator.CalculationVersion, StringComparison.Ordinal);
}

public interface IProjectStateReportingSource
{
    Task<ProjectStateReportingSelection> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectStateReportingSelection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectStateReportingSourceState SourceState,
    ProjectStateReportingClassification Classification,
    DateTimeOffset? LatestApprovedSourceChangedAt,
    ProjectStateReportingSnapshot? SelectedSnapshot,
    IReadOnlyCollection<ProjectStateReportingSnapshot> Trend);

public sealed record ProjectStateReportingSnapshot(
    Guid SnapshotId,
    Guid TenantId,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    ProjectStateReportingClassification Classification,
    string CalculationVersion,
    long ProjectConfigurationRevision,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    DateOnly WindowStart,
    DateOnly WindowEnd,
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
    IReadOnlyCollection<ProjectStateReportingAttentionItem> AttentionItems);

public sealed record ProjectStateReportingAttentionItem(
    Guid SourceReportId,
    Guid SourceFactId,
    DateOnly ReportDate,
    ProjectAttentionKind Kind,
    string Description,
    string? Category,
    Guid? LocationId,
    string? LocationName,
    ProjectObservedImpact? ObservedImpact,
    ProjectAttentionPriority Priority,
    int AgeDays,
    ProjectAttentionAgeBand AgeBand,
    ProjectAttentionStatus Status,
    string? ReferenceCode);

public enum ProjectStateReportingSourceState
{
    NotConfigured = 1,
    Configured = 2
}

public enum ProjectStateReportingClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}
