using Pmcs.Modules.Planning.Domain;

namespace Pmcs.Modules.Planning.Endpoints;

public sealed record CreatePlanningBaselineRequest(
    Guid? ClientGeneratedId,
    string VersionCode,
    string Title,
    PlanningBaselineKind Kind,
    string? SourceSystem,
    string? SourceReference,
    IReadOnlyCollection<PlanningBaselineEntryRequest> Entries);

public sealed record AmendPlanningBaselineRequest(
    string Title,
    string? SourceSystem,
    string? SourceReference,
    IReadOnlyCollection<PlanningBaselineEntryRequest> Entries,
    long BaseRevision);

public sealed record PlanningBaselineEntryRequest(
    Guid? ClientGeneratedId,
    Guid? ParentEntryId,
    string Code,
    string Title,
    PlanningEntryKind Kind,
    ProgressMeasurementMethod MeasurementMethod,
    Guid? MeasurementItemId,
    DateOnly? PlannedStart,
    DateOnly? PlannedFinish,
    decimal? WeightPercent,
    string? ExternalId,
    int SortOrder)
{
    public PlanningBaselineEntry ToDomain() => new(
        ClientGeneratedId.GetValueOrDefault(Guid.NewGuid()),
        ParentEntryId,
        Code,
        Title,
        Kind,
        MeasurementMethod,
        MeasurementItemId,
        PlannedStart,
        PlannedFinish,
        WeightPercent,
        ExternalId,
        SortOrder);
}

public sealed record SubmitPlanningItemRequest(long BaseRevision);

public sealed record ReviewPlanningItemRequest(long BaseRevision, string? Comment);

public sealed record ReturnPlanningItemRequest(long BaseRevision, string Reason);

public sealed record PlanningBaselineResponse(
    Guid Id,
    string VersionCode,
    string Title,
    PlanningBaselineKind Kind,
    string? SourceSystem,
    string? SourceReference,
    PlanningBaselineStatus Status,
    IReadOnlyCollection<PlanningBaselineEntryResponse> Entries,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    long Revision)
{
    public static PlanningBaselineResponse From(PlanningBaseline baseline) => new(
        baseline.Id,
        baseline.VersionCode,
        baseline.Title,
        baseline.Kind,
        baseline.SourceSystem,
        baseline.SourceReference,
        baseline.Status,
        baseline.Entries.OrderBy(entry => entry.SortOrder).Select(PlanningBaselineEntryResponse.From).ToArray(),
        baseline.CreatedBy,
        baseline.CreatedAt,
        baseline.SubmittedAt,
        baseline.ReviewedBy,
        baseline.ReviewedAt,
        baseline.ReviewComment,
        baseline.Revision);
}

public sealed record PlanningBaselineEntryResponse(
    Guid Id,
    Guid? ParentEntryId,
    string Code,
    string Title,
    PlanningEntryKind Kind,
    ProgressMeasurementMethod MeasurementMethod,
    Guid? MeasurementItemId,
    DateOnly? PlannedStart,
    DateOnly? PlannedFinish,
    decimal? WeightPercent,
    string? ExternalId,
    int SortOrder)
{
    public static PlanningBaselineEntryResponse From(PlanningBaselineEntry entry) => new(
        entry.Id,
        entry.ParentEntryId,
        entry.Code,
        entry.Title,
        entry.Kind,
        entry.MeasurementMethod,
        entry.MeasurementItemId,
        entry.PlannedStart,
        entry.PlannedFinish,
        entry.WeightPercent,
        entry.ExternalId,
        entry.SortOrder);
}

public sealed record CreateMilestoneProgressUpdateRequest(
    Guid? ClientGeneratedId,
    Guid BaselineId,
    Guid BaselineEntryId,
    DateOnly StatusDate,
    decimal ProgressPercent,
    string EvidenceReference,
    string? Note);

public sealed record AmendMilestoneProgressUpdateRequest(
    DateOnly StatusDate,
    decimal ProgressPercent,
    string EvidenceReference,
    string? Note,
    long BaseRevision);

public sealed record MilestoneProgressUpdateResponse(
    Guid Id,
    Guid BaselineId,
    Guid BaselineEntryId,
    DateOnly StatusDate,
    decimal ProgressPercent,
    string EvidenceReference,
    string? Note,
    MilestoneProgressStatus Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SubmittedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    long Revision)
{
    public static MilestoneProgressUpdateResponse From(MilestoneProgressUpdate update) => new(
        update.Id,
        update.BaselineId,
        update.BaselineEntryId,
        update.StatusDate,
        update.ProgressPercent,
        update.EvidenceReference,
        update.Note,
        update.Status,
        update.CreatedBy,
        update.CreatedAt,
        update.SubmittedAt,
        update.ReviewedBy,
        update.ReviewedAt,
        update.ReviewComment,
        update.Revision);
}
