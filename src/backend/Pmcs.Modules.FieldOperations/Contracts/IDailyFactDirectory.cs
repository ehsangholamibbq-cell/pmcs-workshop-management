namespace Pmcs.Modules.FieldOperations.Contracts;

public interface IDailyFactDirectory
{
    Task<DailyFactReference?> FindAsync(
        Guid tenantId,
        Guid projectId,
        Guid dailyReportId,
        Guid? dailyFactId,
        CancellationToken cancellationToken = default);

    Task<DailyFactReference?> FindApprovedAttentionFactAsync(
        Guid tenantId,
        Guid projectId,
        Guid dailyFactId,
        CancellationToken cancellationToken = default);
}

public sealed record DailyFactReference(
    Guid DailyReportId,
    Guid? DailyFactId,
    DateOnly ReportDate,
    DailyFactReferenceStatus ReportStatus,
    DailyFactReferenceKind? Kind,
    string? Description,
    string? LocationName,
    DailyFactReferenceImpact? ImpactLevel);

public enum DailyFactReferenceStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Rejected = 5,
    Superseded = 6
}

public enum DailyFactReferenceKind
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

public enum DailyFactReferenceImpact
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
