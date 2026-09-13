using Pmcs.Modules.FieldOperations.Domain;

namespace Pmcs.Modules.FieldOperations.Endpoints;

public sealed record CreateDailyReportRequest(
    Guid? ClientGeneratedId,
    DateOnly ReportDate,
    string? LocationName,
    string? Narrative);

public sealed record AddDailyFactRequest(
    Guid? ClientGeneratedId,
    DailyFactKind Kind,
    string Description,
    string? Category,
    string? LocationName,
    decimal? Quantity,
    string? Unit,
    int? ResourceCount,
    decimal? Hours,
    DailyImpactLevel? ImpactLevel,
    string? ReferenceCode,
    long BaseRevision,
    Guid? MeasurementItemId = null,
    Guid? LocationId = null);

public sealed record SubmitDailyReportRequest(long BaseRevision);

public sealed record ReviewDailyReportRequest(long BaseRevision, string? Comment);

public sealed record StartDailyReportCorrectionRequest(
    Guid ClientGeneratedId,
    long BaseRevision,
    string Reason);

public sealed record ReviseDailyReportDetailsRequest(
    long BaseRevision,
    string? LocationName,
    string? Narrative);

public sealed record RemoveDailyFactRequest(long BaseRevision);

public sealed record DailyFactResponse(
    Guid Id,
    DailyFactKind Kind,
    string Description,
    string? Category,
    string? LocationName,
    Guid? LocationId,
    decimal? Quantity,
    string? Unit,
    int? ResourceCount,
    decimal? Hours,
    DailyImpactLevel? ImpactLevel,
    string? ReferenceCode,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? MeasurementItemId,
    Guid? CopiedFromFactId)
{
    public static DailyFactResponse From(DailyReportFact fact) => new(
        fact.Id,
        fact.Kind,
        fact.Description,
        fact.Category,
        fact.LocationName,
        fact.LocationId,
        fact.Quantity,
        fact.Unit,
        fact.ResourceCount,
        fact.Hours,
        fact.ImpactLevel,
        fact.ReferenceCode,
        fact.CreatedBy,
        fact.CreatedAt,
        fact.MeasurementItemId,
        fact.CopiedFromFactId);
}

public sealed record DailyReportResponse(
    Guid Id,
    Guid ProjectId,
    DateOnly ReportDate,
    string? LocationName,
    string? Narrative,
    DailyReportStatus Status,
    long Revision,
    Guid CreatedBy,
    int FactCount,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    Guid RootReportId,
    int VersionNumber,
    Guid? SupersedesReportId,
    Guid? SupersededByReportId,
    DateTimeOffset? SupersededAt,
    string? CorrectionReason,
    Guid? CorrectionInitiatedBy,
    IReadOnlyCollection<DailyFactResponse>? Facts = null)
{
    public static DailyReportResponse From(DailyReport report, bool includeFacts = false) => new(
        report.Id,
        report.ProjectId,
        report.ReportDate,
        report.LocationName,
        report.Narrative,
        report.Status,
        report.Revision,
        report.CreatedBy,
        report.Facts.Count,
        report.ReviewedBy,
        report.ReviewedAt,
        report.ReviewComment,
        report.RootReportId,
        report.VersionNumber,
        report.SupersedesReportId,
        report.SupersededByReportId,
        report.SupersededAt,
        report.CorrectionReason,
        report.CorrectionInitiatedBy,
        includeFacts ? report.Facts.Select(DailyFactResponse.From).ToArray() : null);
}
