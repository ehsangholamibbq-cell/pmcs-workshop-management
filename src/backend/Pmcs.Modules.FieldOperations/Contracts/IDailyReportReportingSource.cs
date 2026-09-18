namespace Pmcs.Modules.FieldOperations.Contracts;

public interface IDailyReportReportingSource
{
    Task<DailyReportReportingChain?> LoadChainAsync(
        Guid tenantId,
        Guid projectId,
        Guid reportId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);
}

public sealed record DailyReportReportingChain(
    Guid RootReportId,
    Guid? CurrentOfficialReportId,
    DateTimeOffset AsOfUtc,
    IReadOnlyCollection<DailyReportReportingVersion> Versions);

public sealed record DailyReportReportingVersion(
    Guid ReportId,
    Guid RootReportId,
    int VersionNumber,
    Guid? SupersedesReportId,
    Guid? SupersededByReportId,
    DateTimeOffset? SupersededAt,
    DateOnly ReportDate,
    string? LocationName,
    string? Narrative,
    DailyReportReportingVersionState State,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? ApprovedBy,
    DateTimeOffset ApprovedAt,
    DateTimeOffset LastModifiedAt,
    string? CorrectionReason,
    Guid? CorrectionInitiatedBy,
    long Revision,
    IReadOnlyCollection<DailyReportReportingFact> Facts);

public sealed record DailyReportReportingFact(
    Guid FactId,
    Guid? CopiedFromFactId,
    DailyReportReportingFactKind Kind,
    string Description,
    string? Category,
    Guid? LocationId,
    string? LocationName,
    decimal? Quantity,
    string? Unit,
    int? ResourceCount,
    decimal? Hours,
    DailyReportReportingImpactLevel? ImpactLevel,
    string? ReferenceCode,
    Guid? MeasurementItemId,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

public enum DailyReportReportingVersionState
{
    Approved = 1,
    Superseded = 2
}

public enum DailyReportReportingFactKind
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

public enum DailyReportReportingImpactLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public static class DailyReportReportingRules
{
    public static bool IsOfficialAt(
        DateTimeOffset approvedAt,
        DateTimeOffset? supersededAt,
        DateTimeOffset asOfUtc) =>
        approvedAt <= asOfUtc && (!supersededAt.HasValue || supersededAt.Value > asOfUtc);
}
