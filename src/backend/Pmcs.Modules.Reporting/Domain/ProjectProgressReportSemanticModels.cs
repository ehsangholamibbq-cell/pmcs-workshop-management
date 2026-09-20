using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ProjectProgressReportSemanticSnapshot(
    string SchemaVersion,
    string DefinitionCode,
    string DefinitionVersion,
    ReportDataStatus DataStatus,
    IReadOnlyCollection<ProjectProgressReportReasonCode> ReasonCodes,
    ProjectProgressReportParameters Parameters,
    ProjectProgressReportProjectIdentity Project,
    ProjectProgressReportCutoffIdentity Cutoff,
    ReportClassification Classification,
    ProjectProgressReportConfiguration? Configuration,
    ProjectProgressReportBaseline? Baseline,
    ProjectProgressMetricStatus ActualStatus,
    ProjectProgressMetricStatus ScheduleStatus,
    ProjectProgressMetricStatus CurveStatus,
    ProjectProgressCalendarBasis? CalendarBasis,
    ProjectProgressReportSummary? Summary,
    IReadOnlyCollection<ProjectProgressReportEntry> Entries,
    IReadOnlyCollection<ProjectProgressReportMilestone> Milestones,
    ProjectProgressCurveSampling Sampling,
    IReadOnlyCollection<ProjectProgressReportCurvePoint> Curve,
    int ApprovedProgressOutsideBaselineCount,
    DateTimeOffset? SourceMaxChangedAt,
    string SourceManifestSha256);

internal sealed record ProjectProgressReportProjectIdentity(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt);

internal sealed record ProjectProgressReportCutoffIdentity(
    DateTimeOffset SourceCutoffUtc,
    DateOnly CutoffLocalDate);

internal sealed record ProjectProgressReportConfiguration(
    long ConfigurationVersion,
    long ProjectRevision,
    bool ProgressReportingEnabled,
    Pmcs.Modules.Projects.Domain.PlanningMode PlanningMode,
    ProjectProgressCalendarState CalendarState,
    int? WorkingDaysMask,
    long CalendarRevision,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc);

internal sealed record ProjectProgressReportBaseline(
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

internal sealed record ProjectProgressReportSummary(
    decimal? ActualPercent,
    decimal? PlannedPercent,
    decimal? VariancePercent,
    int MissingActualEntryCount);

internal sealed record ProjectProgressReportEntry(
    Guid EntryId,
    Guid? ParentEntryId,
    string Code,
    string Title,
    PlanningEntryKind Kind,
    ProgressMeasurementMethod MeasurementMethod,
    ProjectProgressReportMeasurementTarget? MeasurementTarget,
    DateOnly? PlannedStart,
    DateOnly? PlannedFinish,
    decimal? WeightPercent,
    int SortOrder,
    decimal? ApprovedQuantity,
    decimal? ActualPercent,
    decimal? PlannedPercent,
    decimal? VariancePercent);

internal sealed record ProjectProgressReportMeasurementTarget(
    Guid MeasurementItemId,
    string Code,
    string Title,
    string Unit,
    decimal TargetQuantity);

internal sealed record ProjectProgressReportMilestone(
    Guid BaselineEntryId,
    string Code,
    string Title,
    DateOnly PlannedDate,
    decimal WeightPercent,
    DateOnly? LatestApprovedStatusDate,
    decimal? ApprovedProgressPercent);

internal sealed record ProjectProgressReportCurvePoint(
    DateOnly PointDate,
    decimal? PlannedPercent,
    decimal? ActualPercent,
    decimal? VariancePercent,
    int MissingActualEntryCount);
