using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Planning.Domain;

public static class ProgressLedgerCalculator
{
    public static ProgressLedgerCalculation Calculate(
        PlanningMode planningMode,
        IReadOnlyCollection<MeasurementItemInput> measurementItems,
        IReadOnlyCollection<ProgressObservationInput> observations,
        ApprovedPlanningBaselineInput? approvedBaseline = null,
        IReadOnlyCollection<ApprovedMilestoneProgressInput>? milestoneProgress = null,
        DateOnly? asOfDate = null,
        int? workingDaysMask = null)
    {
        var rows = measurementItems
            .OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var linked = observations.Where(observation => observation.MeasurementItemId == item.Id).ToArray();
                var approvedMeasurements = linked
                    .Where(observation => observation.ReviewState == ProgressReviewState.Approved)
                    .Where(observation => observation.Quantity.HasValue)
                    .ToArray();
                var provisionalMeasurements = linked
                    .Where(observation => observation.ReviewState == ProgressReviewState.Provisional)
                    .Where(observation => observation.Quantity.HasValue)
                    .ToArray();
                var approvedQuantity = approvedMeasurements.Length == 0
                    ? (decimal?)null
                    : approvedMeasurements.Sum(observation => observation.Quantity!.Value);
                var provisionalQuantity = provisionalMeasurements.Length == 0
                    ? (decimal?)null
                    : provisionalMeasurements.Sum(observation => observation.Quantity!.Value);
                var latestApprovedDate = linked
                    .Where(observation => observation.ReviewState == ProgressReviewState.Approved)
                    .Select(observation => (DateOnly?)observation.ReportDate)
                    .Max();
                var completionPercent = item.TargetQuantity.HasValue && approvedQuantity.HasValue
                    ? decimal.Round(approvedQuantity.Value / item.TargetQuantity.Value * 100m, 2, MidpointRounding.AwayFromZero)
                    : (decimal?)null;

                return new ProgressItemCalculation(
                    item.Id,
                    item.Code,
                    item.Title,
                    item.Unit,
                    item.TargetQuantity,
                    item.IsActive,
                    approvedQuantity,
                    provisionalQuantity,
                    completionPercent,
                    latestApprovedDate);
            })
            .ToArray();

        var unlinked = observations.Where(observation => !observation.MeasurementItemId.HasValue).ToArray();
        var calculationDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var milestoneUpdates = milestoneProgress ?? [];
        var basis = CalculateBasis(
            planningMode, approvedBaseline, rows, milestoneUpdates, calculationDate, workingDaysMask);
        return new ProgressLedgerCalculation(
            planningMode,
            !measurementItems.Any(item => item.IsActive)
                ? MeasurementBasisState.NotConfigured
                : measurementItems.Any(item => item.IsActive && item.TargetQuantity.HasValue)
                    ? MeasurementBasisState.TargetsAvailable
                    : MeasurementBasisState.ActualOnly,
            basis.BasisState,
            basis.ScheduleState,
            approvedBaseline?.Id,
            approvedBaseline?.VersionCode,
            approvedBaseline?.Kind,
            basis.OfficialOverallPhysicalPercent,
            basis.PlannedOverallPhysicalPercent,
            basis.ScheduleVariancePercent,
            null,
            basis.MissingActualEntryCount,
            observations.Count(observation => observation.ReviewState == ProgressReviewState.Approved),
            observations.Count(observation => observation.ReviewState == ProgressReviewState.Provisional),
            unlinked.Count(observation => observation.ReviewState == ProgressReviewState.Approved),
            unlinked.Count(observation => observation.ReviewState == ProgressReviewState.Provisional),
            rows,
            basis.Milestones);
    }

    private static BasisCalculation CalculateBasis(
        PlanningMode planningMode,
        ApprovedPlanningBaselineInput? baseline,
        IReadOnlyCollection<ProgressItemCalculation> progressItems,
        IReadOnlyCollection<ApprovedMilestoneProgressInput> milestoneProgress,
        DateOnly asOfDate,
        int? workingDaysMask)
    {
        if (baseline is null)
        {
            return BasisCalculation.NotConfigured;
        }

        if (!Matches(baseline.Kind, planningMode))
        {
            return BasisCalculation.ModeMismatch;
        }

        var entries = baseline.Entries.Where(entry => entry.Kind != PlanningEntryKind.Summary).ToArray();
        if (entries.Length == 0)
        {
            return new BasisCalculation(
                OfficialProgressBasisState.IncompleteActualData,
                baseline.Kind == PlanningBaselineKind.MeasurementWeights
                    ? ScheduleBasisState.NotConfigured
                    : ScheduleBasisState.Approved,
                null,
                null,
                null,
                0,
                []);
        }

        var actuals = entries.Select(entry => new
        {
            Entry = entry,
            Percent = CalculateActualPercent(baseline.Id, entry, progressItems, milestoneProgress)
        }).ToArray();
        var missingActualCount = actuals.Count(item => !item.Percent.HasValue);
        var officialOverall = missingActualCount == 0
            ? decimal.Round(
                actuals.Sum(item => Math.Min(100m, item.Percent!.Value) * item.Entry.WeightPercent!.Value) / 100m,
                2,
                MidpointRounding.AwayFromZero)
            : (decimal?)null;

        var plannedOverall = baseline.Kind == PlanningBaselineKind.MeasurementWeights
            ? (decimal?)null
            : decimal.Round(
                entries.Sum(entry => CalculatePlannedPercent(entry, asOfDate, workingDaysMask) * entry.WeightPercent!.Value) / 100m,
                2,
                MidpointRounding.AwayFromZero);
        var scheduleVariance = officialOverall.HasValue && plannedOverall.HasValue
            ? decimal.Round(officialOverall.Value - plannedOverall.Value, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        var milestones = entries
            .Where(entry => entry.Kind == PlanningEntryKind.Milestone)
            .OrderBy(entry => entry.PlannedFinish)
            .ThenBy(entry => entry.SortOrder)
            .Select(entry =>
            {
                var update = LatestMilestoneUpdate(baseline.Id, entry.Id, milestoneProgress);
                var state = update?.ProgressPercent >= 100m
                    ? MilestoneScheduleState.Completed
                    : entry.PlannedFinish < asOfDate
                        ? MilestoneScheduleState.Late
                        : entry.PlannedFinish == asOfDate
                            ? MilestoneScheduleState.Due
                            : MilestoneScheduleState.Upcoming;
                return new MilestoneCalculation(
                    entry.Id,
                    entry.Code,
                    entry.Title,
                    entry.PlannedFinish!.Value,
                    entry.WeightPercent!.Value,
                    update?.ProgressPercent,
                    update?.StatusDate,
                    state);
            })
            .ToArray();

        return new BasisCalculation(
            missingActualCount == 0
                ? OfficialProgressBasisState.Approved
                : OfficialProgressBasisState.IncompleteActualData,
            baseline.Kind == PlanningBaselineKind.MeasurementWeights
                ? ScheduleBasisState.NotConfigured
                : ScheduleBasisState.Approved,
            officialOverall,
            plannedOverall,
            scheduleVariance,
            missingActualCount,
            milestones);
    }

    private static decimal? CalculateActualPercent(
        Guid baselineId,
        PlanningBaselineEntryInput entry,
        IReadOnlyCollection<ProgressItemCalculation> progressItems,
        IReadOnlyCollection<ApprovedMilestoneProgressInput> milestoneProgress)
    {
        if (entry.MeasurementMethod == ProgressMeasurementMethod.QuantityBased)
        {
            return progressItems
                .SingleOrDefault(item => item.MeasurementItemId == entry.MeasurementItemId)
                ?.ApprovedCompletionPercent;
        }

        return LatestMilestoneUpdate(baselineId, entry.Id, milestoneProgress)?.ProgressPercent;
    }

    private static ApprovedMilestoneProgressInput? LatestMilestoneUpdate(
        Guid baselineId,
        Guid entryId,
        IReadOnlyCollection<ApprovedMilestoneProgressInput> updates) =>
        updates
            .Where(update => update.BaselineId == baselineId && update.BaselineEntryId == entryId)
            .OrderByDescending(update => update.StatusDate)
            .ThenByDescending(update => update.ApprovedAt)
            .FirstOrDefault();

    private static decimal CalculatePlannedPercent(
        PlanningBaselineEntryInput entry,
        DateOnly asOfDate,
        int? workingDaysMask)
    {
        var start = entry.PlannedStart!.Value;
        var finish = entry.PlannedFinish!.Value;
        if (entry.Kind == PlanningEntryKind.Milestone)
        {
            return asOfDate >= finish ? 100m : 0m;
        }

        if (asOfDate < start)
        {
            return 0m;
        }

        if (asOfDate >= finish)
        {
            return 100m;
        }

        var duration = CountPlannedDays(start, finish, workingDaysMask);
        var elapsed = CountPlannedDays(start, asOfDate, workingDaysMask);
        if (duration == 0)
        {
            return 0m;
        }

        return decimal.Round(elapsed * 100m / duration, 4, MidpointRounding.AwayFromZero);
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

    private static bool Matches(PlanningBaselineKind kind, PlanningMode mode) => (kind, mode) switch
    {
        (PlanningBaselineKind.MeasurementWeights, PlanningMode.SimpleWorkList) => true,
        (PlanningBaselineKind.MilestonePlan, PlanningMode.Milestones) => true,
        (PlanningBaselineKind.WbsBaseline, PlanningMode.WbsBaseline) => true,
        (PlanningBaselineKind.ExternalSchedule, PlanningMode.ExternalSchedule) => true,
        _ => false
    };

    private sealed record BasisCalculation(
        OfficialProgressBasisState BasisState,
        ScheduleBasisState ScheduleState,
        decimal? OfficialOverallPhysicalPercent,
        decimal? PlannedOverallPhysicalPercent,
        decimal? ScheduleVariancePercent,
        int MissingActualEntryCount,
        IReadOnlyCollection<MilestoneCalculation> Milestones)
    {
        public static BasisCalculation NotConfigured { get; } = new(
            OfficialProgressBasisState.NotConfigured,
            ScheduleBasisState.NotConfigured,
            null,
            null,
            null,
            0,
            []);

        public static BasisCalculation ModeMismatch { get; } = new(
            OfficialProgressBasisState.ModeMismatch,
            ScheduleBasisState.ModeMismatch,
            null,
            null,
            null,
            0,
            []);
    }
}

public sealed record MeasurementItemInput(
    Guid Id,
    string Code,
    string Title,
    string Unit,
    decimal? TargetQuantity,
    bool IsActive);

public sealed record ProgressObservationInput(
    Guid FactId,
    Guid? MeasurementItemId,
    DateOnly ReportDate,
    ProgressReviewState ReviewState,
    decimal? Quantity);

public sealed record ProgressLedgerCalculation(
    PlanningMode PlanningMode,
    MeasurementBasisState MeasurementBasisState,
    OfficialProgressBasisState OfficialProgressBasisState,
    ScheduleBasisState ScheduleBasisState,
    Guid? ApprovedBaselineId,
    string? ApprovedBaselineVersion,
    PlanningBaselineKind? ApprovedBaselineKind,
    decimal? OfficialOverallPhysicalPercent,
    decimal? PlannedOverallPhysicalPercent,
    decimal? ScheduleVariancePercent,
    DateOnly? ForecastCompletionDate,
    int MissingActualEntryCount,
    int ApprovedFactCount,
    int ProvisionalFactCount,
    int UnlinkedApprovedFactCount,
    int UnlinkedProvisionalFactCount,
    IReadOnlyCollection<ProgressItemCalculation> Items,
    IReadOnlyCollection<MilestoneCalculation> Milestones);

public sealed record ProgressItemCalculation(
    Guid MeasurementItemId,
    string Code,
    string Title,
    string Unit,
    decimal? TargetQuantity,
    bool IsActive,
    decimal? ApprovedQuantity,
    decimal? ProvisionalQuantity,
    decimal? ApprovedCompletionPercent,
    DateOnly? LatestApprovedReportDate);

public enum ProgressReviewState
{
    Provisional = 1,
    Approved = 2
}

public enum MeasurementBasisState
{
    NotConfigured = 1,
    ActualOnly = 2,
    TargetsAvailable = 3
}

public sealed record ApprovedPlanningBaselineInput(
    Guid Id,
    string VersionCode,
    PlanningBaselineKind Kind,
    IReadOnlyCollection<PlanningBaselineEntryInput> Entries);

public sealed record PlanningBaselineEntryInput(
    Guid Id,
    string Code,
    string Title,
    PlanningEntryKind Kind,
    ProgressMeasurementMethod MeasurementMethod,
    Guid? MeasurementItemId,
    DateOnly? PlannedStart,
    DateOnly? PlannedFinish,
    decimal? WeightPercent,
    int SortOrder);

public sealed record ApprovedMilestoneProgressInput(
    Guid BaselineId,
    Guid BaselineEntryId,
    DateOnly StatusDate,
    decimal ProgressPercent,
    DateTimeOffset ApprovedAt);

public sealed record MilestoneCalculation(
    Guid BaselineEntryId,
    string Code,
    string Title,
    DateOnly PlannedDate,
    decimal WeightPercent,
    decimal? ApprovedProgressPercent,
    DateOnly? LatestApprovedStatusDate,
    MilestoneScheduleState ScheduleState);

public enum OfficialProgressBasisState
{
    NotConfigured = 1,
    Approved = 2,
    IncompleteActualData = 3,
    ModeMismatch = 4
}

public enum ScheduleBasisState
{
    NotConfigured = 1,
    Approved = 2,
    ModeMismatch = 3
}

public enum MilestoneScheduleState
{
    Upcoming = 1,
    Due = 2,
    Late = 3,
    Completed = 4
}
