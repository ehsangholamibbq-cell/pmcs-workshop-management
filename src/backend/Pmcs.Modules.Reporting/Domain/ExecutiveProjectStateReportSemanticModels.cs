using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ExecutiveProjectStateReportSemanticSnapshot(
    string SchemaVersion,
    string DefinitionCode,
    string DefinitionVersion,
    ReportDataStatus DataStatus,
    IReadOnlyCollection<ExecutiveProjectStateReportReasonCode> ReasonCodes,
    ExecutiveProjectStateReportParameters Parameters,
    ExecutiveProjectStateProjectIdentity Project,
    ExecutiveProjectStateCutoffIdentity Cutoff,
    ProjectStateReportingSourceState SourceState,
    ExecutiveProjectStateSnapshot? OfficialSnapshot,
    ExecutiveProjectStateCurrency Currency,
    IReadOnlyCollection<ExecutiveProjectStateTrendPoint> Trend,
    string SourceManifestSha256);

internal sealed record ExecutiveProjectStateProjectIdentity(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    string BaseCurrencyCode,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt);

internal sealed record ExecutiveProjectStateCutoffIdentity(
    DateTimeOffset SourceCutoffUtc,
    DateOnly CutoffLocalDate);

internal sealed record ExecutiveProjectStateCurrency(
    bool? ProjectConfigurationCurrent,
    bool? ApprovedSourceCurrent,
    DateTimeOffset? LatestApprovedSourceChangedAt);

internal sealed record ExecutiveProjectStateSnapshot(
    Guid SnapshotId,
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
    ExecutiveProjectStateFactCounts FactCounts,
    ExecutiveProjectStateAttentionSummary AttentionSummary,
    ExecutiveProjectStateFeatureStates FeatureStates,
    DateTimeOffset? SourceMaxChangedAt,
    IReadOnlyCollection<ExecutiveProjectStateAttentionItem> AttentionItems);

internal sealed record ExecutiveProjectStateFactCounts(
    int Approved,
    int Progress,
    int Labor,
    int Equipment,
    int Material);

internal sealed record ExecutiveProjectStateAttentionSummary(
    int Issues,
    int Stoppages,
    int High,
    int Critical,
    int? OldestAgeDays);

internal sealed record ExecutiveProjectStateFeatureStates(
    ProjectFeatureState Contract,
    ProjectFeatureState Planning,
    ProjectFeatureState Budget,
    ProjectFeatureState Quality,
    ProjectFeatureState Hse);

internal sealed record ExecutiveProjectStateAttentionItem(
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

internal sealed record ExecutiveProjectStateTrendPoint(
    Guid SnapshotId,
    string CalculationVersion,
    DateOnly AsOfDate,
    DateTimeOffset CalculatedAt,
    ProjectOperationalStatus OperationalStatus,
    DataCoverageStatus CoverageStatus,
    DataFreshnessStatus FreshnessStatus,
    DataConfidenceStatus ConfidenceStatus,
    decimal CoveragePercent,
    int IssueCount,
    int StoppageCount,
    int HighImpactCount,
    int CriticalImpactCount);
