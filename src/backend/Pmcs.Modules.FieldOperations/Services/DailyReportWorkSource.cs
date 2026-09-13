using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class DailyReportWorkSource(FieldOperationsDbContext dbContext) : IDailyReportWorkSource
{
    public async Task<IReadOnlyCollection<DailyReportWorkRecord>> ListAsync(
        Guid tenantId,
        Guid projectId,
        Guid userId,
        bool includeReviewQueue,
        CancellationToken cancellationToken = default)
    {
        var reports = await dbContext.DailyReports
            .AsNoTracking()
            .Where(report => report.TenantId == tenantId && report.ProjectId == projectId &&
                ((includeReviewQueue && report.Status == DailyReportStatus.Submitted) ||
                    (report.CreatedBy == userId && report.Status == DailyReportStatus.Returned) ||
                    (report.CreatedBy == userId && report.Status == DailyReportStatus.Draft &&
                        report.SupersedesReportId != null)))
            .OrderBy(report => report.ReportDate)
            .ThenBy(report => report.VersionNumber)
            .Take(200)
            .ToArrayAsync(cancellationToken);

        return reports.Select(report => new DailyReportWorkRecord(
            report.Id,
            report.ReportDate,
            report.VersionNumber,
            report.Status,
            ResolveKind(report),
            report.CorrectionReason ?? report.ReviewComment,
            report.LastModifiedAt,
            report.Revision)).ToArray();
    }

    private static DailyReportWorkKind ResolveKind(DailyReport report) => report.Status switch
    {
        DailyReportStatus.Submitted => DailyReportWorkKind.Review,
        DailyReportStatus.Returned => DailyReportWorkKind.CorrectReturned,
        DailyReportStatus.Draft when report.SupersedesReportId.HasValue => DailyReportWorkKind.CompleteCorrection,
        _ => throw new InvalidOperationException("Unsupported daily report work state.")
    };
}
