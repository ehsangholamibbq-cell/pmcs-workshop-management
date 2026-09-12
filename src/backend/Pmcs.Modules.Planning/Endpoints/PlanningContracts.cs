using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Planning.Endpoints;

public sealed record CreateMeasurementItemRequest(
    Guid? ClientGeneratedId,
    string Code,
    string Title,
    string Unit,
    decimal? TargetQuantity,
    string? Notes);

public sealed record AmendMeasurementItemRequest(
    string Title,
    decimal? TargetQuantity,
    string? Notes,
    long BaseRevision);

public sealed record DeactivateMeasurementItemRequest(long BaseRevision);

public sealed record MeasurementItemResponse(
    Guid Id,
    string Code,
    string Title,
    string Unit,
    decimal? TargetQuantity,
    string? Notes,
    MeasurementItemStatus Status,
    long Revision,
    DateTimeOffset LastModifiedAt)
{
    public static MeasurementItemResponse From(MeasurementItem item) => new(
        item.Id,
        item.Code,
        item.Title,
        item.Unit,
        item.TargetQuantity,
        item.Notes,
        item.Status,
        item.Revision,
        item.LastModifiedAt);
}

public sealed record ProgressLedgerResponse(
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
    IReadOnlyCollection<ProgressItemResponse> Items,
    IReadOnlyCollection<MilestoneProgressSummaryResponse> Milestones)
{
    public static ProgressLedgerResponse From(ProgressLedgerCalculation calculation) => new(
        calculation.PlanningMode,
        calculation.MeasurementBasisState,
        calculation.OfficialProgressBasisState,
        calculation.ScheduleBasisState,
        calculation.ApprovedBaselineId,
        calculation.ApprovedBaselineVersion,
        calculation.ApprovedBaselineKind,
        calculation.OfficialOverallPhysicalPercent,
        calculation.PlannedOverallPhysicalPercent,
        calculation.ScheduleVariancePercent,
        calculation.ForecastCompletionDate,
        calculation.MissingActualEntryCount,
        calculation.ApprovedFactCount,
        calculation.ProvisionalFactCount,
        calculation.UnlinkedApprovedFactCount,
        calculation.UnlinkedProvisionalFactCount,
        calculation.Items.Select(ProgressItemResponse.From).ToArray(),
        calculation.Milestones.Select(MilestoneProgressSummaryResponse.From).ToArray());
}

public sealed record ProgressItemResponse(
    Guid MeasurementItemId,
    string Code,
    string Title,
    string Unit,
    decimal? TargetQuantity,
    bool IsActive,
    decimal? ApprovedQuantity,
    decimal? ProvisionalQuantity,
    decimal? ApprovedCompletionPercent,
    DateOnly? LatestApprovedReportDate)
{
    public static ProgressItemResponse From(ProgressItemCalculation item) => new(
        item.MeasurementItemId,
        item.Code,
        item.Title,
        item.Unit,
        item.TargetQuantity,
        item.IsActive,
        item.ApprovedQuantity,
        item.ProvisionalQuantity,
        item.ApprovedCompletionPercent,
        item.LatestApprovedReportDate);
}

public sealed record MilestoneProgressSummaryResponse(
    Guid BaselineEntryId,
    string Code,
    string Title,
    DateOnly PlannedDate,
    decimal WeightPercent,
    decimal? ApprovedProgressPercent,
    DateOnly? LatestApprovedStatusDate,
    MilestoneScheduleState ScheduleState)
{
    public static MilestoneProgressSummaryResponse From(MilestoneCalculation item) => new(
        item.BaselineEntryId,
        item.Code,
        item.Title,
        item.PlannedDate,
        item.WeightPercent,
        item.ApprovedProgressPercent,
        item.LatestApprovedStatusDate,
        item.ScheduleState);
}
