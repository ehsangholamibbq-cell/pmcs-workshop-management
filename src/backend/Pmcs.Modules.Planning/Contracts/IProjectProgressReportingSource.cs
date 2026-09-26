using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Planning.Contracts;

public interface IProjectProgressReportingSource
{
    Task<ProjectProgressReportingResult> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default);
}

public static class ProjectProgressReportingContract
{
    public const string Version = "pmcs.planning.project-progress-reporting/v1";
    public const string SourceManifestVersion = "pmcs.planning.project-progress-manifest/v1";
    public const int MaximumBaselineEntries = 5_000;
    public const int MaximumCurvePoints = 366;
    public const int UniformCurveGridPoints = 365;
}

public sealed record ProjectProgressReportingProjection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    IReadOnlyCollection<ProjectProgressConfigurationVersion> Configurations,
    IReadOnlyCollection<ProjectProgressBaselineVersion> Baselines,
    IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion> MilestoneUpdates,
    ProgressEvidenceReportingProjection Evidence);

public sealed record ProjectProgressConfigurationVersion(
    long ConfigurationVersion,
    long ProjectRevision,
    bool ProgressReportingEnabled,
    PlanningMode PlanningMode,
    ProjectProgressCalendarState CalendarState,
    int? WorkingDaysMask,
    long CalendarRevision,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    ProjectProgressReportingClassification Classification);

public sealed record ProjectProgressBaselineVersion(
    Guid BaselineId,
    string VersionCode,
    string Title,
    PlanningBaselineKind Kind,
    long ApprovalRevision,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt,
    string? SourceSystem,
    string? SourceReference,
    ProjectProgressReportingClassification Classification,
    IReadOnlyCollection<ProjectProgressBaselineEntry> Entries);

public sealed record ProjectProgressBaselineEntry(
    Guid EntryId,
    Guid? ParentEntryId,
    string Code,
    string Title,
    PlanningEntryKind Kind,
    ProgressMeasurementMethod MeasurementMethod,
    Guid? MeasurementItemId,
    DateOnly? PlannedStart,
    DateOnly? PlannedFinish,
    decimal? WeightPercent,
    int SortOrder,
    ProjectProgressPinnedMeasurementTarget? PinnedTarget);

public sealed record ProjectProgressPinnedMeasurementTarget(
    Guid MeasurementItemId,
    string Code,
    string Title,
    string Unit,
    decimal TargetQuantity,
    long Revision,
    DateTimeOffset CapturedAtUtc);

public sealed record ProjectProgressMilestoneUpdateVersion(
    Guid UpdateId,
    Guid BaselineId,
    Guid BaselineEntryId,
    DateOnly StatusDate,
    decimal ProgressPercent,
    long ApprovalRevision,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt,
    ProjectProgressReportingClassification Classification);

public sealed record ProjectProgressReportingSelection(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectProgressConfigurationVersion? Configuration,
    ProjectProgressBaselineVersion? Baseline,
    IReadOnlyCollection<ProjectProgressBaselineVersion> BaselineLineage,
    IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion> MilestoneUpdates,
    ProgressEvidenceReportingProjection Evidence,
    ProjectProgressReportingClassification Classification,
    DateTimeOffset? SourceMaxChangedAt,
    ProjectProgressSourceManifest SourceManifest,
    string SourceManifestSha256);

public sealed record ProjectProgressReportingResult(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectProgressReportingClassification Classification,
    ProjectProgressReportingDataStatus DataStatus,
    IReadOnlyCollection<ProjectProgressReportingReasonCode> ReasonCodes,
    ProjectProgressConfigurationVersion? Configuration,
    ProjectProgressBaselineIdentity? Baseline,
    ProjectProgressMetricStatus ActualStatus,
    ProjectProgressMetricStatus ScheduleStatus,
    ProjectProgressMetricStatus CurveStatus,
    ProjectProgressCalendarBasis? CalendarBasis,
    ProjectProgressSummary? Summary,
    IReadOnlyCollection<ProjectProgressEntryResult> Entries,
    IReadOnlyCollection<ProjectProgressMilestoneResult> Milestones,
    ProjectProgressCurveSampling Sampling,
    IReadOnlyCollection<ProjectProgressCurvePoint> Curve,
    int ApprovedProgressOutsideBaselineCount,
    DateTimeOffset? SourceMaxChangedAt,
    ProjectProgressSourceManifest SourceManifest,
    string SourceManifestSha256);

public sealed record ProjectProgressBaselineIdentity(
    Guid BaselineId,
    string VersionCode,
    string Title,
    PlanningBaselineKind Kind,
    long ApprovalRevision,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt,
    string? SourceSystem,
    string? SourceReference,
    string DefinitionSha256);

public sealed record ProjectProgressSummary(
    decimal? ActualPercent,
    decimal? PlannedPercent,
    decimal? VariancePercent,
    int MissingActualEntryCount);

public sealed record ProjectProgressEntryResult(
    Guid EntryId,
    Guid? ParentEntryId,
    string Code,
    string Title,
    PlanningEntryKind Kind,
    ProgressMeasurementMethod MeasurementMethod,
    Guid? MeasurementItemId,
    string? MeasurementCode,
    string? MeasurementTitle,
    string? Unit,
    decimal? TargetQuantity,
    DateOnly? PlannedStart,
    DateOnly? PlannedFinish,
    decimal? WeightPercent,
    int SortOrder,
    decimal? ApprovedQuantity,
    decimal? ActualPercent,
    decimal? PlannedPercent,
    decimal? VariancePercent);

public sealed record ProjectProgressMilestoneResult(
    Guid BaselineEntryId,
    string Code,
    string Title,
    DateOnly PlannedDate,
    decimal WeightPercent,
    DateOnly? LatestApprovedStatusDate,
    decimal? ApprovedProgressPercent);

public sealed record ProjectProgressCurvePoint(
    DateOnly PointDate,
    decimal? PlannedPercent,
    decimal? ActualPercent,
    decimal? VariancePercent,
    int MissingActualEntryCount);

public sealed record ProjectProgressCurveSampling(
    ProjectProgressCurveSamplingKind Kind,
    bool IsSampled,
    int BaseGridPointCount,
    int PointCount);

public sealed record ProjectProgressSourceManifest(
    string ManifestVersion,
    string SourceContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc,
    ProjectProgressConfigurationManifest? Configuration,
    IReadOnlyCollection<ProjectProgressBaselineManifest> Baselines,
    IReadOnlyCollection<ProjectProgressMilestoneManifest> MilestoneUpdates,
    IReadOnlyCollection<ProjectProgressEvidenceRootManifest> EvidenceRoots);

public sealed record ProjectProgressConfigurationManifest(
    long ConfigurationVersion,
    long ProjectRevision,
    PlanningMode PlanningMode,
    ProjectProgressCalendarState CalendarState,
    int? WorkingDaysMask,
    long CalendarRevision,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc);

public sealed record ProjectProgressBaselineManifest(
    Guid BaselineId,
    string VersionCode,
    PlanningBaselineKind Kind,
    long ApprovalRevision,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt,
    string DefinitionSha256);

public sealed record ProjectProgressMilestoneManifest(
    Guid UpdateId,
    Guid BaselineId,
    Guid BaselineEntryId,
    DateOnly StatusDate,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt);

public sealed record ProjectProgressEvidenceRootManifest(
    Guid RootReportId,
    DateOnly ReportDate,
    Guid? CurrentOfficialReportId,
    IReadOnlyCollection<ProjectProgressEvidenceVersionManifest> Versions);

public sealed record ProjectProgressEvidenceVersionManifest(
    Guid ReportId,
    int VersionNumber,
    DateTimeOffset ApprovedAt,
    DateTimeOffset? SupersededAt,
    IReadOnlyCollection<Guid> ProgressFactIds);

public enum ProjectProgressReportingClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

public enum ProjectProgressCalendarState
{
    NotConfigured = 1,
    WorkingWeek = 2
}

public enum ProjectProgressReportingDataStatus
{
    NotConfigured = 1,
    NoData = 2,
    InsufficientData = 3,
    Available = 4
}

public enum ProjectProgressMetricStatus
{
    NotConfigured = 1,
    NoData = 2,
    InsufficientData = 3,
    Available = 4
}

public enum ProjectProgressCalendarBasis
{
    CalendarDays = 1,
    WorkingDays = 2
}

public enum ProjectProgressCurveSamplingKind
{
    NotConfigured = 1,
    DailyInclusiveV1 = 2,
    Uniform365PlusCutoffV1 = 3
}

public enum ProjectProgressReportingReasonCode
{
    ProgressReportingNotConfigured = 1,
    PlanningModeNone = 2,
    OfficialBaselineMissing = 3,
    PlanningModeBaselineMismatch = 4,
    OfficialActualMissing = 5,
    OfficialActualIncomplete = 6,
    ScheduleNotConfiguredForMeasurementWeights = 7,
    CalendarDaysFallback = 8,
    ApprovedProgressOutsideBaseline = 9
}
