namespace Pmcs.Modules.FieldOperations.Contracts;

public interface IApprovedDailyFactSource
{
    Task<ApprovedDailyFactSet> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly fromDate,
        DateOnly throughDate,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLatestApprovedChangeAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLatestApprovedChangeAtAsync(
        Guid tenantId,
        Guid projectId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLatestApprovedChangesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default);
}

public sealed record ApprovedDailyFactSet(
    DateOnly? LastApprovedReportDate,
    DateTimeOffset? LatestApprovedChangeAt,
    IReadOnlyCollection<ApprovedDailyReportRecord> Reports);

public sealed record ApprovedDailyReportRecord(
    Guid ReportId,
    DateOnly ReportDate,
    DateTimeOffset ApprovedAt,
    IReadOnlyCollection<ApprovedDailyFactRecord> Facts);

public sealed record ApprovedDailyFactRecord(
    Guid FactId,
    ApprovedDailyFactKind Kind,
    string Description,
    string? Category,
    string? LocationName,
    decimal? Quantity,
    string? Unit,
    int? ResourceCount,
    decimal? Hours,
    ApprovedDailyImpactLevel? ImpactLevel,
    string? ReferenceCode,
    Guid? MeasurementItemId = null,
    Guid? LocationId = null);

public enum ApprovedDailyFactKind
{
    WorkProgress = 1,
    Labor = 2,
    Equipment = 3,
    Material = 4,
    Issue = 5,
    Stoppage = 6,
    SiteCondition = 7,
    Note = 8
}

public enum ApprovedDailyImpactLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
