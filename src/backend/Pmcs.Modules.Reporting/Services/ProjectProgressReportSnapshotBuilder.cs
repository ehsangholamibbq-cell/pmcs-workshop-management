using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectProgressReportSnapshotBuilder
{
    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ProjectControlProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectProgressReportingResult source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt) => Build(
        runId,
        tenantId,
        ProjectProgressPinnedProjectProfile.Capture(project),
        sourceCutoffUtc,
        source,
        validatedAtUtc,
        builtAt);

    public static ReportSnapshot Build(
        Guid runId,
        Guid tenantId,
        ProjectProgressPinnedProjectProfile project,
        DateTimeOffset sourceCutoffUtc,
        ProjectProgressReportingResult source,
        DateTimeOffset validatedAtUtc,
        DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var validatedAt = validatedAtUtc.ToUniversalTime();
        var timeZone = project.ValidateForRun(tenantId, project.Id, cutoff, validatedAt);
        var cutoffLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, timeZone).DateTime);
        var canonical = ValidateAndCanonicalizeSource(
            tenantId,
            project.Id,
            cutoffLocalDate,
            cutoff,
            project.ConfigurationVersion,
            source);
        var sourceManifestJson = CanonicalJson.Serialize(canonical.SourceManifest);
        var sourceManifestSha256 = CanonicalJson.Sha256(sourceManifestJson);
        if (!string.Equals(sourceManifestSha256, canonical.SourceManifestSha256, StringComparison.Ordinal))
        {
            throw Invalid(
                "source_manifest.hash_mismatch",
                "The Planning source manifest hash does not match its canonical content.");
        }

        var classification = MapClassification(canonical.Classification);
        var payload = new ProjectProgressReportSemanticSnapshot(
            ProjectProgressReportRuntimeContract.SnapshotSchemaVersion,
            ProjectProgressReportRuntimeContract.DefinitionCode,
            ProjectProgressReportRuntimeContract.DefinitionVersion,
            MapDataStatus(canonical.DataStatus),
            canonical.ReasonCodes.Select(MapReason).OrderBy(reason => reason).ToArray(),
            new ProjectProgressReportParameters(),
            new ProjectProgressReportProjectIdentity(
                project.Id,
                project.TenantId,
                project.Code,
                project.Name,
                project.TimeZone,
                project.Revision,
                project.ConfigurationVersion,
                project.ConfigurationChangedAt.ToUniversalTime()),
            new ProjectProgressReportCutoffIdentity(cutoff, cutoffLocalDate),
            classification,
            MapConfiguration(canonical.Configuration),
            MapBaseline(canonical.Baseline),
            canonical.ActualStatus,
            canonical.ScheduleStatus,
            canonical.CurveStatus,
            canonical.CalendarBasis,
            canonical.Summary is null
                ? null
                : new ProjectProgressReportSummary(
                    canonical.Summary.ActualPercent,
                    canonical.Summary.PlannedPercent,
                    canonical.Summary.VariancePercent,
                    canonical.Summary.MissingActualEntryCount),
            canonical.Entries.Select(MapEntry).ToArray(),
            canonical.Milestones.Select(item => new ProjectProgressReportMilestone(
                item.BaselineEntryId,
                item.Code,
                item.Title,
                item.PlannedDate,
                item.WeightPercent,
                item.LatestApprovedStatusDate,
                item.ApprovedProgressPercent)).ToArray(),
            canonical.Sampling,
            canonical.Curve.Select(point => new ProjectProgressReportCurvePoint(
                point.PointDate,
                point.PlannedPercent,
                point.ActualPercent,
                point.VariancePercent,
                point.MissingActualEntryCount)).ToArray(),
            canonical.ApprovedProgressOutsideBaselineCount,
            canonical.SourceMaxChangedAt,
            sourceManifestSha256);

        return ReportSnapshot.Create(
            Guid.NewGuid(),
            runId,
            tenantId,
            project.Id,
            ProjectProgressReportRuntimeContract.SnapshotSchemaVersion,
            MapDataStatus(canonical.DataStatus),
            CanonicalJson.Serialize(payload),
            sourceManifestJson,
            classification,
            builtAt,
            cutoff);
    }

    private static ProjectProgressReportingResult ValidateAndCanonicalizeSource(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        long currentProjectConfigurationVersion,
        ProjectProgressReportingResult source)
    {
        if (!string.Equals(source.ContractVersion, ProjectProgressReportingContract.Version, StringComparison.Ordinal) ||
            source.TenantId != tenantId || source.ProjectId != projectId ||
            source.CutoffLocalDate != cutoffLocalDate ||
            source.SourceCutoffUtc.ToUniversalTime() != sourceCutoffUtc ||
            !Enum.IsDefined(source.Classification) || !Enum.IsDefined(source.DataStatus) ||
            !Enum.IsDefined(source.ActualStatus) || !Enum.IsDefined(source.ScheduleStatus) ||
            !Enum.IsDefined(source.CurveStatus) ||
            (source.CalendarBasis.HasValue && !Enum.IsDefined(source.CalendarBasis.Value)) ||
            source.ReasonCodes is null || source.Entries is null || source.Milestones is null ||
            source.Curve is null || source.SourceManifest is null ||
            string.IsNullOrWhiteSpace(source.SourceManifestSha256) ||
            source.ApprovedProgressOutsideBaselineCount < 0 ||
            source.SourceMaxChangedAt?.ToUniversalTime() > sourceCutoffUtc)
        {
            throw Invalid("source.invalid", "The Planning progress source violates its versioned scope contract.");
        }

        if (source.Configuration is not null &&
            (source.Configuration.ConfigurationVersion > currentProjectConfigurationVersion ||
                source.Configuration.EffectiveFromUtc.ToUniversalTime() > sourceCutoffUtc ||
                source.Configuration.EffectiveToUtc?.ToUniversalTime() <= sourceCutoffUtc ||
                !Enum.IsDefined(source.Configuration.PlanningMode) ||
                !Enum.IsDefined(source.Configuration.CalendarState) ||
                !Enum.IsDefined(source.Configuration.Classification)))
        {
            throw Invalid(
                "configuration.invalid",
                "The effective Planning configuration is not valid for this project and cutoff.");
        }

        var reasons = source.ReasonCodes.OrderBy(reason => reason).ToArray();
        if (reasons.Any(reason => !Enum.IsDefined(reason)) || reasons.Distinct().Count() != reasons.Length)
        {
            throw Invalid("reason.invalid", "Progress report reason codes must be unique and allowlisted.");
        }

        ValidateStatus(source, reasons);
        ValidateManifest(source, tenantId, projectId, cutoffLocalDate, sourceCutoffUtc);
        var entries = ValidateEntries(source);
        var milestones = ValidateMilestones(source);
        var curve = ValidateCurve(source, cutoffLocalDate);
        ValidateSummary(source, curve);

        return source with
        {
            SourceCutoffUtc = sourceCutoffUtc,
            ReasonCodes = reasons,
            SourceMaxChangedAt = source.SourceMaxChangedAt?.ToUniversalTime(),
            Entries = entries,
            Milestones = milestones,
            Curve = curve
        };
    }

    private static void ValidateStatus(
        ProjectProgressReportingResult source,
        IReadOnlyCollection<ProjectProgressReportingReasonCode> reasons)
    {
        var valid = source.DataStatus switch
        {
            ProjectProgressReportingDataStatus.NotConfigured =>
                source.Baseline is null && source.Summary is null && source.Entries.Count == 0 &&
                source.Curve.Count == 0 &&
                (reasons.SequenceEqual([ProjectProgressReportingReasonCode.ProgressReportingNotConfigured]) ||
                    reasons.SequenceEqual([ProjectProgressReportingReasonCode.PlanningModeNone])),
            ProjectProgressReportingDataStatus.NoData =>
                source.Baseline is null && source.Summary is null && source.Entries.Count == 0 &&
                reasons.SequenceEqual([ProjectProgressReportingReasonCode.OfficialBaselineMissing]),
            ProjectProgressReportingDataStatus.InsufficientData =>
                source.Baseline is not null && reasons.Any(reason => reason is
                    ProjectProgressReportingReasonCode.PlanningModeBaselineMismatch or
                    ProjectProgressReportingReasonCode.OfficialActualMissing or
                    ProjectProgressReportingReasonCode.OfficialActualIncomplete),
            ProjectProgressReportingDataStatus.Available =>
                source.Baseline is not null && source.Summary?.ActualPercent is not null &&
                !reasons.Any(reason => reason is
                    ProjectProgressReportingReasonCode.PlanningModeBaselineMismatch or
                    ProjectProgressReportingReasonCode.OfficialActualMissing or
                    ProjectProgressReportingReasonCode.OfficialActualIncomplete),
            _ => false
        };
        if (!valid)
        {
            throw Invalid("status.invalid", "Progress data status is inconsistent with its source and reasons.");
        }

        var scheduleNotConfigured = reasons.Contains(
            ProjectProgressReportingReasonCode.ScheduleNotConfiguredForMeasurementWeights);
        if (scheduleNotConfigured !=
            (source.ScheduleStatus == ProjectProgressMetricStatus.NotConfigured &&
                source.CurveStatus == ProjectProgressMetricStatus.NotConfigured &&
                source.Baseline?.Kind == Pmcs.Modules.Planning.Domain.PlanningBaselineKind.MeasurementWeights))
        {
            throw Invalid("schedule_status.invalid", "Schedule and curve status do not match the baseline kind.");
        }
    }

    private static void ValidateManifest(
        ProjectProgressReportingResult source,
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc)
    {
        var manifest = source.SourceManifest;
        if (!string.Equals(
                manifest.ManifestVersion,
                ProjectProgressReportingContract.SourceManifestVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.SourceContractVersion,
                ProjectProgressReportingContract.Version,
                StringComparison.Ordinal) ||
            manifest.TenantId != tenantId || manifest.ProjectId != projectId ||
            manifest.CutoffLocalDate != cutoffLocalDate ||
            manifest.SourceCutoffUtc.ToUniversalTime() != sourceCutoffUtc ||
            manifest.Baselines is null || manifest.MilestoneUpdates is null || manifest.EvidenceRoots is null)
        {
            throw Invalid("source_manifest.invalid", "The progress source manifest violates its scope contract.");
        }
    }

    private static ProjectProgressEntryResult[] ValidateEntries(ProjectProgressReportingResult source)
    {
        if (source.Entries.Count > ProjectProgressReportingContract.MaximumBaselineEntries)
        {
            throw Invalid("entry.limit_exceeded", "The semantic progress entry budget was exceeded.");
        }

        var entries = source.Entries
            .OrderBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Code, StringComparer.Ordinal)
            .ThenBy(entry => entry.EntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (entries.Select(entry => entry.EntryId).Distinct().Count() != entries.Length ||
            entries.Any(entry => entry.EntryId == Guid.Empty || entry.SortOrder <= 0 ||
                string.IsNullOrWhiteSpace(entry.Code) || string.IsNullOrWhiteSpace(entry.Title) ||
                !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.MeasurementMethod) ||
                entry.ApprovedQuantity < 0 || entry.MissingTarget()))
        {
            throw Invalid("entry.invalid", "A semantic progress entry is invalid or duplicated.");
        }

        foreach (var entry in entries)
        {
            ValidateVariance(entry.ActualPercent, entry.PlannedPercent, entry.VariancePercent, "entry");
        }
        return entries;
    }

    private static ProjectProgressMilestoneResult[] ValidateMilestones(ProjectProgressReportingResult source)
    {
        var milestones = source.Milestones
            .OrderBy(item => item.PlannedDate)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.BaselineEntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (milestones.Select(item => item.BaselineEntryId).Distinct().Count() != milestones.Length ||
            milestones.Any(item => item.BaselineEntryId == Guid.Empty ||
                string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Title) ||
                item.PlannedDate == default || item.WeightPercent is <= 0 or > 100 ||
                item.ApprovedProgressPercent is < 0 or > 100))
        {
            throw Invalid("milestone.invalid", "A semantic milestone row is invalid or duplicated.");
        }
        return milestones;
    }

    private static ProjectProgressCurvePoint[] ValidateCurve(
        ProjectProgressReportingResult source,
        DateOnly cutoffLocalDate)
    {
        if (!Enum.IsDefined(source.Sampling.Kind) || source.Sampling.BaseGridPointCount < 0 ||
            source.Sampling.PointCount != source.Curve.Count ||
            source.Curve.Count > ProjectProgressReportingContract.MaximumCurvePoints)
        {
            throw Invalid("curve.invalid", "The S-Curve sampling contract is invalid.");
        }

        var curve = source.Curve.OrderBy(point => point.PointDate).ToArray();
        if (curve.Select(point => point.PointDate).Distinct().Count() != curve.Length ||
            curve.Any(point => point.PointDate == default || point.MissingActualEntryCount < 0 ||
                (point.PointDate > cutoffLocalDate &&
                    (point.ActualPercent.HasValue || point.VariancePercent.HasValue))))
        {
            throw Invalid("curve.point.invalid", "An S-Curve point violates ordering or future-actual rules.");
        }
        foreach (var point in curve)
        {
            ValidateVariance(point.ActualPercent, point.PlannedPercent, point.VariancePercent, "curve");
        }
        if (curve.Length > 0 && curve.All(point => point.PointDate != cutoffLocalDate))
        {
            throw Invalid("curve.cutoff_missing", "A configured S-Curve must contain the cutoff date.");
        }
        return curve;
    }

    private static void ValidateSummary(
        ProjectProgressReportingResult source,
        IReadOnlyCollection<ProjectProgressCurvePoint> curve)
    {
        if (source.Summary is null)
        {
            return;
        }
        if (source.Summary.MissingActualEntryCount < 0)
        {
            throw Invalid("summary.invalid", "The progress summary has an invalid missing-entry count.");
        }
        ValidateVariance(
            source.Summary.ActualPercent,
            source.Summary.PlannedPercent,
            source.Summary.VariancePercent,
            "summary");
        var cutoffPoint = curve.SingleOrDefault(point => point.PointDate == source.CutoffLocalDate);
        if (cutoffPoint is not null &&
            (cutoffPoint.ActualPercent != source.Summary.ActualPercent ||
                cutoffPoint.PlannedPercent != source.Summary.PlannedPercent ||
                cutoffPoint.VariancePercent != source.Summary.VariancePercent ||
                cutoffPoint.MissingActualEntryCount != source.Summary.MissingActualEntryCount))
        {
            throw Invalid("summary.curve_mismatch", "The cutoff summary must match the S-Curve cutoff point.");
        }
    }

    private static void ValidateVariance(
        decimal? actual,
        decimal? planned,
        decimal? variance,
        string scope)
    {
        var expected = actual.HasValue && planned.HasValue
            ? decimal.Round(actual.Value - planned.Value, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        if (variance != expected)
        {
            throw Invalid($"{scope}.variance.invalid", "Variance must equal Actual minus Planned.");
        }
    }

    private static ProjectProgressReportConfiguration? MapConfiguration(
        ProjectProgressConfigurationVersion? configuration) => configuration is null
        ? null
        : new ProjectProgressReportConfiguration(
            configuration.ConfigurationVersion,
            configuration.ProjectRevision,
            configuration.ProgressReportingEnabled,
            configuration.PlanningMode,
            configuration.CalendarState,
            configuration.WorkingDaysMask,
            configuration.CalendarRevision,
            configuration.EffectiveFromUtc,
            configuration.EffectiveToUtc);

    private static ProjectProgressReportBaseline? MapBaseline(ProjectProgressBaselineIdentity? baseline) =>
        baseline is null
            ? null
            : new ProjectProgressReportBaseline(
                baseline.BaselineId,
                baseline.VersionCode,
                baseline.Title,
                baseline.Kind,
                baseline.ApprovalRevision,
                baseline.ApprovedAt,
                baseline.SupersededAt,
                baseline.SourceSystem,
                baseline.SourceReference,
                baseline.DefinitionSha256);

    private static ProjectProgressReportEntry MapEntry(ProjectProgressEntryResult entry) => new(
        entry.EntryId,
        entry.ParentEntryId,
        entry.Code,
        entry.Title,
        entry.Kind,
        entry.MeasurementMethod,
        entry.MeasurementItemId.HasValue
            ? new ProjectProgressReportMeasurementTarget(
                entry.MeasurementItemId.Value,
                entry.MeasurementCode!,
                entry.MeasurementTitle!,
                entry.Unit!,
                entry.TargetQuantity!.Value)
            : null,
        entry.PlannedStart,
        entry.PlannedFinish,
        entry.WeightPercent,
        entry.SortOrder,
        entry.ApprovedQuantity,
        entry.ActualPercent,
        entry.PlannedPercent,
        entry.VariancePercent);

    private static ProjectProgressReportReasonCode MapReason(ProjectProgressReportingReasonCode reason) => reason switch
    {
        ProjectProgressReportingReasonCode.ProgressReportingNotConfigured =>
            ProjectProgressReportReasonCode.ProgressReportingNotConfigured,
        ProjectProgressReportingReasonCode.PlanningModeNone => ProjectProgressReportReasonCode.PlanningModeNone,
        ProjectProgressReportingReasonCode.OfficialBaselineMissing =>
            ProjectProgressReportReasonCode.OfficialBaselineMissing,
        ProjectProgressReportingReasonCode.PlanningModeBaselineMismatch =>
            ProjectProgressReportReasonCode.PlanningModeBaselineMismatch,
        ProjectProgressReportingReasonCode.OfficialActualMissing =>
            ProjectProgressReportReasonCode.OfficialActualMissing,
        ProjectProgressReportingReasonCode.OfficialActualIncomplete =>
            ProjectProgressReportReasonCode.OfficialActualIncomplete,
        ProjectProgressReportingReasonCode.ScheduleNotConfiguredForMeasurementWeights =>
            ProjectProgressReportReasonCode.ScheduleNotConfiguredForMeasurementWeights,
        ProjectProgressReportingReasonCode.CalendarDaysFallback =>
            ProjectProgressReportReasonCode.CalendarDaysFallback,
        ProjectProgressReportingReasonCode.ApprovedProgressOutsideBaseline =>
            ProjectProgressReportReasonCode.ApprovedProgressOutsideBaseline,
        _ => throw Invalid("reason.unknown", "The Planning source returned an unknown reason code.")
    };

    private static ReportDataStatus MapDataStatus(ProjectProgressReportingDataStatus status) => status switch
    {
        ProjectProgressReportingDataStatus.NotConfigured => ReportDataStatus.NotConfigured,
        ProjectProgressReportingDataStatus.NoData => ReportDataStatus.NoData,
        ProjectProgressReportingDataStatus.InsufficientData => ReportDataStatus.InsufficientData,
        ProjectProgressReportingDataStatus.Available => ReportDataStatus.Available,
        _ => throw Invalid("status.unknown", "The Planning source returned an unknown data status.")
    };

    private static ReportClassification MapClassification(ProjectProgressReportingClassification classification) =>
        classification switch
        {
            ProjectProgressReportingClassification.Internal => ReportClassification.Internal,
            ProjectProgressReportingClassification.Confidential => ReportClassification.Confidential,
            ProjectProgressReportingClassification.Restricted => ReportClassification.Restricted,
            _ => throw Invalid("classification.unknown", "The Planning source classification is unknown.")
        };

    private static bool MissingTarget(this ProjectProgressEntryResult entry) =>
        entry.MeasurementItemId.HasValue &&
        (string.IsNullOrWhiteSpace(entry.MeasurementCode) ||
            string.IsNullOrWhiteSpace(entry.MeasurementTitle) ||
            string.IsNullOrWhiteSpace(entry.Unit) || !entry.TargetQuantity.HasValue ||
            entry.TargetQuantity.Value <= 0);

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"reporting.project_progress.{suffix}", message);
}
