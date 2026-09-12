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
    Guid? MeasurementItemId = null);

public sealed record SubmitDailyReportRequest(long BaseRevision);

public sealed record ReviewDailyReportRequest(long BaseRevision, string? Comment);

public sealed record DailyFactResponse(
    Guid Id,
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
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? MeasurementItemId)
{
    public static DailyFactResponse From(DailyReportFact fact) => new(
        fact.Id,
        fact.Kind,
        fact.Description,
        fact.Category,
        fact.LocationName,
        fact.Quantity,
        fact.Unit,
        fact.ResourceCount,
        fact.Hours,
        fact.ImpactLevel,
        fact.ReferenceCode,
        fact.CreatedBy,
        fact.CreatedAt,
        fact.MeasurementItemId);
}

public sealed record DailyReportResponse(
    Guid Id,
    Guid ProjectId,
    DateOnly ReportDate,
    string? LocationName,
    string? Narrative,
    DailyReportStatus Status,
    long Revision,
    int FactCount,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
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
        report.Facts.Count,
        report.ReviewedBy,
        report.ReviewedAt,
        report.ReviewComment,
        includeFacts ? report.Facts.Select(DailyFactResponse.From).ToArray() : null);
}
