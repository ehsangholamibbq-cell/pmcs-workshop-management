using Pmcs.Modules.FieldOperations.Domain;

namespace Pmcs.Modules.FieldOperations.Contracts;

public interface IDailyReportWorkSource
{
    Task<IReadOnlyCollection<DailyReportWorkRecord>> ListAsync(
        Guid tenantId,
        Guid projectId,
        Guid userId,
        bool includeReviewQueue,
        CancellationToken cancellationToken = default);
}

public sealed record DailyReportWorkRecord(
    Guid ReportId,
    DateOnly ReportDate,
    int VersionNumber,
    DailyReportStatus Status,
    DailyReportWorkKind Kind,
    string? CorrectionReason,
    DateTimeOffset ChangedAt,
    long Revision);

public enum DailyReportWorkKind
{
    Review = 1,
    CorrectReturned = 2,
    CompleteCorrection = 3
}
