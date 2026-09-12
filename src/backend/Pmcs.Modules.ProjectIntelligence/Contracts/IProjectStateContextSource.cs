using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.ProjectIntelligence.Contracts;

public interface IProjectStateContextSource
{
    Task<ProjectStateContextRecord?> GetLatestAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectStateContextRecord(
    Guid SnapshotId,
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string CalculationVersion,
    long ProjectConfigurationRevision,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    string AssessmentScope,
    bool IsPartial,
    string OperationalStatus,
    string CoverageStatus,
    string FreshnessStatus,
    string ConfidenceStatus,
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
    IReadOnlyCollection<ProjectStateAttentionContextRecord> AttentionItems);

public sealed record ProjectStateAttentionContextRecord(
    Guid SourceReportId,
    Guid SourceFactId,
    DateOnly ReportDate,
    string Kind,
    string Description,
    string? Category,
    string? LocationName,
    string? ObservedImpact,
    string Priority,
    int AgeDays,
    string Status,
    string? ReferenceCode);
