using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Planning.Services;

internal static class ProjectProgressReportingCalculator
{
    public static ProjectProgressReportingResult Calculate(ProjectProgressReportingSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ValidateSelection(selection);

        if (selection.Configuration is null || !selection.Configuration.ProgressReportingEnabled)
        {
            return Empty(
                selection,
                ProjectProgressReportingDataStatus.NotConfigured,
                ProjectProgressReportingReasonCode.ProgressReportingNotConfigured);
        }

        if (selection.Configuration.PlanningMode == PlanningMode.None)
        {
            return Empty(
                selection,
                ProjectProgressReportingDataStatus.NotConfigured,
                ProjectProgressReportingReasonCode.PlanningModeNone);
        }

        if (selection.Baseline is null)
        {
            return Empty(
                selection,
                ProjectProgressReportingDataStatus.NoData,
                ProjectProgressReportingReasonCode.OfficialBaselineMissing,
                ProjectProgressMetricStatus.NoData);
        }

        var baseline = selection.Baseline;
        if (!Matches(baseline.Kind, selection.Configuration.PlanningMode))
        {
            return Mismatch(selection, baseline);
        }

        var weightedEntries = baseline.Entries
            .Where(entry => entry.Kind != PlanningEntryKind.Summary)
            .ToArray();
        var officialFacts = CurrentOfficialFacts(selection.Evidence);
        var officialMilestones = selection.MilestoneUpdates
            .Where(update => update.BaselineId == baseline.BaselineId &&
                IsEffectiveAt(update.ApprovedAt, update.SupersededAt, selection.SourceCutoffUtc))
            .ToArray();
        var scheduled = baseline.Kind != PlanningBaselineKind.MeasurementWeights;
        var calendarBasis = scheduled
            ? selection.Configuration.CalendarState == ProjectProgressCalendarState.WorkingWeek
                ? ProjectProgressCalendarBasis.WorkingDays
                : ProjectProgressCalendarBasis.CalendarDays
            : (ProjectProgressCalendarBasis?)null;
        var cutoffCalculation = CalculateAt(
            weightedEntries,
            officialFacts,
            officialMilestones,
            selection.CutoffLocalDate,
            includeActual: true,
            scheduled,
            selection.Configuration.WorkingDaysMask);
        var outsideBaselineCount = CountOutsideBaseline(weightedEntries, officialFacts);
        var reasons = new HashSet<ProjectProgressReportingReasonCode>();
        if (cutoffCalculation.MissingActualEntryCount == weightedEntries.Length)
        {
            reasons.Add(ProjectProgressReportingReasonCode.OfficialActualMissing);
        }
        else if (cutoffCalculation.MissingActualEntryCount > 0)
        {
            reasons.Add(ProjectProgressReportingReasonCode.OfficialActualIncomplete);
        }
        if (!scheduled)
        {
            reasons.Add(ProjectProgressReportingReasonCode.ScheduleNotConfiguredForMeasurementWeights);
        }
        else if (calendarBasis == ProjectProgressCalendarBasis.CalendarDays)
        {
            reasons.Add(ProjectProgressReportingReasonCode.CalendarDaysFallback);
        }
        if (outsideBaselineCount > 0)
        {
            reasons.Add(ProjectProgressReportingReasonCode.ApprovedProgressOutsideBaseline);
        }

        var actualStatus = cutoffCalculation.MissingActualEntryCount == weightedEntries.Length
            ? ProjectProgressMetricStatus.NoData
            : cutoffCalculation.MissingActualEntryCount > 0
                ? ProjectProgressMetricStatus.InsufficientData
                : ProjectProgressMetricStatus.Available;
        var dataStatus = actualStatus == ProjectProgressMetricStatus.Available
            ? ProjectProgressReportingDataStatus.Available
            : ProjectProgressReportingDataStatus.InsufficientData;
        var curve = scheduled
            ? BuildCurve(selection, weightedEntries, officialFacts, officialMilestones)
            : new CurveCalculation(
                new ProjectProgressCurveSampling(
                    ProjectProgressCurveSamplingKind.NotConfigured,
                    IsSampled: false,
                    BaseGridPointCount: 0,
                    PointCount: 0),
                []);

        return new ProjectProgressReportingResult(
            ProjectProgressReportingContract.Version,
            selection.TenantId,
            selection.ProjectId,
            selection.CutoffLocalDate,
            selection.SourceCutoffUtc,
            selection.Classification,
            dataStatus,
            reasons.OrderBy(reason => reason).ToArray(),
            selection.Configuration,
            Identity(baseline),
            actualStatus,
            scheduled ? ProjectProgressMetricStatus.Available : ProjectProgressMetricStatus.NotConfigured,
            scheduled ? ProjectProgressMetricStatus.Available : ProjectProgressMetricStatus.NotConfigured,
            calendarBasis,
            new ProjectProgressSummary(
                cutoffCalculation.ActualPercent,
                cutoffCalculation.PlannedPercent,
                cutoffCalculation.VariancePercent,
                cutoffCalculation.MissingActualEntryCount),
            BuildRows(baseline, cutoffCalculation),
            BuildMilestones(baseline, officialMilestones, selection.CutoffLocalDate),
            curve.Sampling,
            curve.Points,
            outsideBaselineCount,
            selection.SourceMaxChangedAt,
            selection.SourceManifest,
            selection.SourceManifestSha256);
    }

    private static void ValidateSelection(ProjectProgressReportingSelection selection)
    {
        if (!string.Equals(selection.ContractVersion, ProjectProgressReportingContract.Version, StringComparison.Ordinal) ||
            selection.TenantId == Guid.Empty || selection.ProjectId == Guid.Empty ||
            selection.CutoffLocalDate == default || selection.SourceCutoffUtc == default ||
            !Enum.IsDefined(selection.Classification) || selection.BaselineLineage is null ||
            selection.MilestoneUpdates is null || selection.Evidence is null ||
            selection.SourceManifest is null || string.IsNullOrWhiteSpace(selection.SourceManifestSha256) ||
            !string.Equals(
                selection.SourceManifestSha256,
                ProjectProgressCanonicalJson.Sha256(
                    ProjectProgressCanonicalJson.Serialize(selection.SourceManifest)),
                StringComparison.Ordinal))
        {
            throw Invalid("selection.invalid", "The selected progress source violates its versioned contract.");
        }
    }

    private static ProjectProgressReportingResult Empty(
        ProjectProgressReportingSelection selection,
        ProjectProgressReportingDataStatus dataStatus,
        ProjectProgressReportingReasonCode reason,
        ProjectProgressMetricStatus metricStatus = ProjectProgressMetricStatus.NotConfigured) => new(
        ProjectProgressReportingContract.Version,
        selection.TenantId,
        selection.ProjectId,
        selection.CutoffLocalDate,
        selection.SourceCutoffUtc,
        selection.Classification,
        dataStatus,
        [reason],
        selection.Configuration,
        null,
        metricStatus,
        metricStatus,
        metricStatus,
        null,
        null,
        [],
        [],
        new ProjectProgressCurveSampling(
            ProjectProgressCurveSamplingKind.NotConfigured,
            IsSampled: false,
            BaseGridPointCount: 0,
            PointCount: 0),
        [],
        0,
        selection.SourceMaxChangedAt,
        selection.SourceManifest,
        selection.SourceManifestSha256);

    private static ProjectProgressReportingResult Mismatch(
        ProjectProgressReportingSelection selection,
        ProjectProgressBaselineVersion baseline) => new(
        ProjectProgressReportingContract.Version,
        selection.TenantId,
        selection.ProjectId,
        selection.CutoffLocalDate,
        selection.SourceCutoffUtc,
        selection.Classification,
        ProjectProgressReportingDataStatus.InsufficientData,
        [ProjectProgressReportingReasonCode.PlanningModeBaselineMismatch],
        selection.Configuration,
        Identity(baseline),
        ProjectProgressMetricStatus.InsufficientData,
        ProjectProgressMetricStatus.InsufficientData,
        ProjectProgressMetricStatus.InsufficientData,
        null,
        null,
        baseline.Entries.Select(EmptyRow).ToArray(),
        [],
        new ProjectProgressCurveSampling(
            ProjectProgressCurveSamplingKind.NotConfigured,
            IsSampled: false,
            BaseGridPointCount: 0,
            PointCount: 0),
        [],
        0,
        selection.SourceMaxChangedAt,
        selection.SourceManifest,
        selection.SourceManifestSha256);

    private static ProjectProgressBaselineIdentity Identity(ProjectProgressBaselineVersion baseline) => new(
        baseline.BaselineId,
        baseline.VersionCode,
        baseline.Title,
        baseline.Kind,
        baseline.ApprovalRevision,
        baseline.ApprovedAt,
        baseline.SupersededAt,
        baseline.SourceSystem,
        baseline.SourceReference,
        ProjectProgressCanonicalJson.Sha256(ProjectProgressCanonicalJson.Serialize(baseline.Entries)));

    private static ProjectProgressEntryResult[] BuildRows(
        ProjectProgressBaselineVersion baseline,
        PointCalculation calculation)
    {
        var calculated = calculation.Entries.ToDictionary(entry => entry.EntryId);
        return baseline.Entries.Select(entry => entry.Kind == PlanningEntryKind.Summary
            ? EmptyRow(entry)
            : MapRow(entry, calculated[entry.EntryId])).ToArray();
    }

    private static ProjectProgressEntryResult EmptyRow(ProjectProgressBaselineEntry entry) => new(
        entry.EntryId,
        entry.ParentEntryId,
        entry.Code,
        entry.Title,
        entry.Kind,
        entry.MeasurementMethod,
        entry.MeasurementItemId,
        entry.PinnedTarget?.Code,
        entry.PinnedTarget?.Title,
        entry.PinnedTarget?.Unit,
        entry.PinnedTarget?.TargetQuantity,
        entry.PlannedStart,
        entry.PlannedFinish,
        entry.WeightPercent,
        entry.SortOrder,
        null,
        null,
        null,
        null);

    private static ProjectProgressEntryResult MapRow(
        ProjectProgressBaselineEntry entry,
        EntryCalculation calculation) => new(
        entry.EntryId,
        entry.ParentEntryId,
        entry.Code,
        entry.Title,
        entry.Kind,
        entry.MeasurementMethod,
        entry.MeasurementItemId,
        entry.PinnedTarget?.Code,
        entry.PinnedTarget?.Title,
        entry.PinnedTarget?.Unit,
        entry.PinnedTarget?.TargetQuantity,
        entry.PlannedStart,
        entry.PlannedFinish,
        entry.WeightPercent,
        entry.SortOrder,
        calculation.ApprovedQuantity,
        calculation.ActualPercent,
        calculation.PlannedPercent,
        calculation.VariancePercent);

    private static ProjectProgressMilestoneResult[] BuildMilestones(
        ProjectProgressBaselineVersion baseline,
        IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion> updates,
        DateOnly cutoffLocalDate) =>
        baseline.Entries
            .Where(entry => entry.Kind == PlanningEntryKind.Milestone)
            .Select(entry =>
            {
                var latest = LatestMilestoneUpdate(
                    baseline.BaselineId,
                    entry.EntryId,
                    cutoffLocalDate,
                    updates);
                return new ProjectProgressMilestoneResult(
                    entry.EntryId,
                    entry.Code,
                    entry.Title,
                    entry.PlannedFinish!.Value,
                    entry.WeightPercent!.Value,
                    latest?.StatusDate,
                    latest?.ProgressPercent);
            })
            .OrderBy(item => item.PlannedDate)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.BaselineEntryId.ToString("D"), StringComparer.Ordinal)
            .ToArray();

    private static CurveCalculation BuildCurve(
        ProjectProgressReportingSelection selection,
        IReadOnlyCollection<ProjectProgressBaselineEntry> entries,
        IReadOnlyCollection<OfficialProgressFact> facts,
        IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion> milestoneUpdates)
    {
        var dates = BuildCurveDates(entries, selection.CutoffLocalDate);
        var kind = dates.IsSampled
            ? ProjectProgressCurveSamplingKind.Uniform365PlusCutoffV1
            : ProjectProgressCurveSamplingKind.DailyInclusiveV1;
        var points = dates.Dates.Select(pointDate =>
        {
            var includeActual = pointDate <= selection.CutoffLocalDate;
            var point = CalculateAt(
                entries,
                facts,
                milestoneUpdates,
                pointDate,
                includeActual,
                scheduled: true,
                selection.Configuration!.WorkingDaysMask);
            return new ProjectProgressCurvePoint(
                pointDate,
                point.PlannedPercent,
                point.ActualPercent,
                point.VariancePercent,
                point.MissingActualEntryCount);
        }).ToArray();
        if (points.Length > ProjectProgressReportingContract.MaximumCurvePoints)
        {
            throw Invalid("curve.limit_exceeded", "The deterministic S-Curve exceeds its versioned point budget.");
        }

        return new CurveCalculation(
            new ProjectProgressCurveSampling(
                kind,
                dates.IsSampled,
                dates.BaseGridPointCount,
                points.Length),
            points);
    }

    private static CurveDates BuildCurveDates(
        IReadOnlyCollection<ProjectProgressBaselineEntry> entries,
        DateOnly cutoffLocalDate)
    {
        var earliest = entries.Min(entry => entry.PlannedStart!.Value);
        var latest = entries.Max(entry => entry.PlannedFinish!.Value);
        var start = earliest < cutoffLocalDate ? earliest : cutoffLocalDate;
        var end = latest > cutoffLocalDate ? latest : cutoffLocalDate;
        var spanDays = end.DayNumber - start.DayNumber;
        if (spanDays + 1 <= ProjectProgressReportingContract.MaximumCurvePoints)
        {
            return new CurveDates(
                Enumerable.Range(0, spanDays + 1).Select(start.AddDays).ToArray(),
                IsSampled: false,
                BaseGridPointCount: spanDays + 1);
        }

        var grid = Enumerable.Range(0, ProjectProgressReportingContract.UniformCurveGridPoints)
            .Select(index => start.AddDays((int)((long)index * spanDays /
                (ProjectProgressReportingContract.UniformCurveGridPoints - 1))))
            .Append(cutoffLocalDate)
            .Distinct()
            .OrderBy(date => date)
            .ToArray();
        return new CurveDates(
            grid,
            IsSampled: true,
            BaseGridPointCount: ProjectProgressReportingContract.UniformCurveGridPoints);
    }

    private static PointCalculation CalculateAt(
        IReadOnlyCollection<ProjectProgressBaselineEntry> entries,
        IReadOnlyCollection<OfficialProgressFact> facts,
        IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion> milestoneUpdates,
        DateOnly pointDate,
        bool includeActual,
        bool scheduled,
        int? workingDaysMask)
    {
        var calculations = entries.Select(entry =>
        {
            decimal? quantity = null;
            decimal? actual = null;
            if (includeActual && entry.MeasurementMethod == ProgressMeasurementMethod.QuantityBased)
            {
                var linked = facts
                    .Where(fact => fact.MeasurementItemId == entry.MeasurementItemId && fact.ReportDate <= pointDate)
                    .ToArray();
                if (linked.Any(fact => fact.Quantity.HasValue))
                {
                    var expectedUnit = entry.PinnedTarget!.Unit;
                    if (linked.Any(fact => fact.Quantity.HasValue &&
                        !string.Equals(fact.Unit, expectedUnit, StringComparison.Ordinal)))
                    {
                        throw Invalid(
                            "evidence.unit_mismatch",
                            "Official progress evidence does not match the approval-time target unit.");
                    }

                    quantity = linked.Where(fact => fact.Quantity.HasValue).Sum(fact => fact.Quantity!.Value);
                    actual = decimal.Round(
                        quantity.Value / entry.PinnedTarget.TargetQuantity * 100m,
                        2,
                        MidpointRounding.AwayFromZero);
                }
            }
            else if (includeActual && entry.MeasurementMethod == ProgressMeasurementMethod.ManualPercent)
            {
                actual = LatestMilestoneUpdate(
                    null,
                    entry.EntryId,
                    pointDate,
                    milestoneUpdates)?.ProgressPercent;
            }

            var planned = scheduled
                ? CalculatePlannedPercent(entry, pointDate, workingDaysMask)
                : (decimal?)null;
            var variance = actual.HasValue && planned.HasValue
                ? decimal.Round(actual.Value - planned.Value, 2, MidpointRounding.AwayFromZero)
                : (decimal?)null;
            return new EntryCalculation(entry.EntryId, quantity, actual, planned, variance);
        }).ToArray();
        var missingCount = includeActual
            ? calculations.Count(item => !item.ActualPercent.HasValue)
            : entries.Count;
        var overallActual = includeActual && missingCount == 0
            ? decimal.Round(
                calculations.Sum(item =>
                    Math.Min(100m, item.ActualPercent!.Value) *
                    entries.Single(entry => entry.EntryId == item.EntryId).WeightPercent!.Value) / 100m,
                2,
                MidpointRounding.AwayFromZero)
            : (decimal?)null;
        var overallPlanned = scheduled
            ? decimal.Round(
                calculations.Sum(item =>
                    item.PlannedPercent!.Value *
                    entries.Single(entry => entry.EntryId == item.EntryId).WeightPercent!.Value) / 100m,
                2,
                MidpointRounding.AwayFromZero)
            : (decimal?)null;
        var overallVariance = overallActual.HasValue && overallPlanned.HasValue
            ? decimal.Round(overallActual.Value - overallPlanned.Value, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        return new PointCalculation(
            overallActual,
            overallPlanned,
            overallVariance,
            missingCount,
            calculations);
    }

    private static decimal CalculatePlannedPercent(
        ProjectProgressBaselineEntry entry,
        DateOnly pointDate,
        int? workingDaysMask)
    {
        var start = entry.PlannedStart!.Value;
        var finish = entry.PlannedFinish!.Value;
        if (entry.Kind == PlanningEntryKind.Milestone)
        {
            return pointDate >= finish ? 100m : 0m;
        }
        if (pointDate < start)
        {
            return 0m;
        }
        if (pointDate >= finish)
        {
            return 100m;
        }

        var duration = CountPlannedDays(start, finish, workingDaysMask);
        var elapsed = CountPlannedDays(start, pointDate, workingDaysMask);
        return duration == 0
            ? 0m
            : decimal.Round(elapsed * 100m / duration, 4, MidpointRounding.AwayFromZero);
    }

    private static int CountPlannedDays(DateOnly start, DateOnly finish, int? workingDaysMask)
    {
        if (!workingDaysMask.HasValue)
        {
            return finish.DayNumber - start.DayNumber + 1;
        }

        var totalDays = finish.DayNumber - start.DayNumber + 1;
        var workingDaysPerWeek = Enumerable.Range(0, 7)
            .Count(day => (workingDaysMask.Value & (1 << day)) != 0);
        var count = totalDays / 7 * workingDaysPerWeek;
        var remainder = totalDays % 7;
        for (var offset = 0; offset < remainder; offset++)
        {
            if ((workingDaysMask.Value & (1 << (int)start.AddDays(offset).DayOfWeek)) != 0)
            {
                count++;
            }
        }
        return count;
    }

    private static ProjectProgressMilestoneUpdateVersion? LatestMilestoneUpdate(
        Guid? baselineId,
        Guid entryId,
        DateOnly pointDate,
        IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion> updates) =>
        updates
            .Where(update => (!baselineId.HasValue || update.BaselineId == baselineId.Value) &&
                update.BaselineEntryId == entryId && update.StatusDate <= pointDate)
            .OrderByDescending(update => update.StatusDate)
            .ThenByDescending(update => update.ApprovedAt)
            .ThenByDescending(update => update.UpdateId.ToString("D"), StringComparer.Ordinal)
            .FirstOrDefault();

    private static OfficialProgressFact[] CurrentOfficialFacts(ProgressEvidenceReportingProjection evidence) =>
        evidence.Roots
            .Where(root => root.CurrentOfficialReportId.HasValue)
            .Select(root => new
            {
                root.ReportDate,
                Version = root.Versions.Single(version => version.ReportId == root.CurrentOfficialReportId!.Value)
            })
            .SelectMany(item => item.Version.Facts.Select(fact => new OfficialProgressFact(
                fact.FactId,
                item.ReportDate,
                fact.MeasurementItemId,
                fact.Quantity,
                fact.Unit)))
            .OrderBy(fact => fact.ReportDate)
            .ThenBy(fact => fact.FactId.ToString("D"), StringComparer.Ordinal)
            .ToArray();

    private static int CountOutsideBaseline(
        IReadOnlyCollection<ProjectProgressBaselineEntry> entries,
        IReadOnlyCollection<OfficialProgressFact> facts)
    {
        var mapped = entries
            .Where(entry => entry.MeasurementMethod == ProgressMeasurementMethod.QuantityBased &&
                entry.MeasurementItemId.HasValue)
            .Select(entry => entry.MeasurementItemId!.Value)
            .ToHashSet();
        return facts.Count(fact => !fact.MeasurementItemId.HasValue || !mapped.Contains(fact.MeasurementItemId.Value));
    }

    private static bool Matches(PlanningBaselineKind kind, PlanningMode mode) => (kind, mode) switch
    {
        (PlanningBaselineKind.MeasurementWeights, PlanningMode.SimpleWorkList) => true,
        (PlanningBaselineKind.MilestonePlan, PlanningMode.Milestones) => true,
        (PlanningBaselineKind.WbsBaseline, PlanningMode.WbsBaseline) => true,
        (PlanningBaselineKind.ExternalSchedule, PlanningMode.ExternalSchedule) => true,
        _ => false
    };

    private static bool IsEffectiveAt(
        DateTimeOffset approvedAt,
        DateTimeOffset? supersededAt,
        DateTimeOffset cutoff) =>
        approvedAt <= cutoff && (!supersededAt.HasValue || cutoff < supersededAt.Value);

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"planning.progress_reporting.{suffix}", message);

    private sealed record OfficialProgressFact(
        Guid FactId,
        DateOnly ReportDate,
        Guid? MeasurementItemId,
        decimal? Quantity,
        string? Unit);

    private sealed record EntryCalculation(
        Guid EntryId,
        decimal? ApprovedQuantity,
        decimal? ActualPercent,
        decimal? PlannedPercent,
        decimal? VariancePercent);

    private sealed record PointCalculation(
        decimal? ActualPercent,
        decimal? PlannedPercent,
        decimal? VariancePercent,
        int MissingActualEntryCount,
        IReadOnlyCollection<EntryCalculation> Entries);

    private sealed record CurveDates(
        IReadOnlyCollection<DateOnly> Dates,
        bool IsSampled,
        int BaseGridPointCount);

    private sealed record CurveCalculation(
        ProjectProgressCurveSampling Sampling,
        IReadOnlyCollection<ProjectProgressCurvePoint> Points);
}
