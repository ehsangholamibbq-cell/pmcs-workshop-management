using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class DailyFactDirectory(FieldOperationsDbContext dbContext) : IDailyFactDirectory
{
    public async Task<DailyFactReference?> FindAsync(
        Guid tenantId,
        Guid projectId,
        Guid dailyReportId,
        Guid? dailyFactId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.DailyReports
            .AsNoTracking()
            .Include(item => item.Facts)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId &&
                item.ProjectId == projectId && item.Id == dailyReportId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        var fact = dailyFactId.HasValue
            ? report.Facts.SingleOrDefault(item => item.Id == dailyFactId.Value)
            : null;
        return dailyFactId.HasValue && fact is null ? null : Map(report, fact);
    }

    public async Task<DailyFactReference?> FindApprovedAttentionFactAsync(
        Guid tenantId,
        Guid projectId,
        Guid dailyFactId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.DailyReports
            .AsNoTracking()
            .Include(item => item.Facts)
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId &&
                item.Status == DailyReportStatus.Approved)
            .SingleOrDefaultAsync(item => item.Facts.Any(fact => fact.Id == dailyFactId), cancellationToken);
        var fact = report?.Facts.SingleOrDefault(item => item.Id == dailyFactId);
        return report is null || fact is null || fact.Kind is not DailyFactKind.Issue and not DailyFactKind.Stoppage
            ? null
            : Map(report, fact);
    }

    private static DailyFactReference Map(DailyReport report, DailyReportFact? fact) => new(
        report.Id,
        fact?.Id,
        report.ReportDate,
        Enum.Parse<DailyFactReferenceStatus>(report.Status.ToString()),
        fact is null ? null : Enum.Parse<DailyFactReferenceKind>(fact.Kind.ToString()),
        fact?.Description,
        fact?.LocationName,
        fact?.ImpactLevel is null
            ? null
            : Enum.Parse<DailyFactReferenceImpact>(fact.ImpactLevel.Value.ToString()));
}
