using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class ApprovedDailyFactSource(FieldOperationsDbContext dbContext) : IApprovedDailyFactSource
{
    public async Task<ApprovedDailyFactSet> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly fromDate,
        DateOnly throughDate,
        CancellationToken cancellationToken = default)
    {
        var approved = dbContext.DailyReports
            .AsNoTracking()
            .Where(report => report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                report.Status == DailyReportStatus.Approved &&
                report.ReportDate <= throughDate);

        var lastApprovedReportDate = await approved
            .OrderByDescending(report => report.ReportDate)
            .Select(report => (DateOnly?)report.ReportDate)
            .FirstOrDefaultAsync(cancellationToken);
        var latestApprovedChangeAt = await approved
            .OrderByDescending(report => report.LastModifiedAt)
            .Select(report => (DateTimeOffset?)report.LastModifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var reports = await approved
            .Include(report => report.Facts)
            .Where(report => report.ReportDate >= fromDate)
            .OrderBy(report => report.ReportDate)
            .ToListAsync(cancellationToken);

        return new ApprovedDailyFactSet(
            lastApprovedReportDate,
            latestApprovedChangeAt,
            reports.Select(Map).ToArray());
    }

    public Task<DateTimeOffset?> GetLatestApprovedChangeAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default) =>
        dbContext.DailyReports
            .AsNoTracking()
            .Where(report => report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                report.Status == DailyReportStatus.Approved)
            .OrderByDescending(report => report.LastModifiedAt)
            .Select(report => (DateTimeOffset?)report.LastModifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DateTimeOffset?> GetLatestApprovedChangeAtAsync(
        Guid tenantId,
        Guid projectId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = asOfUtc.ToUniversalTime();
        var versions = await dbContext.DailyReports
            .AsNoTracking()
            .Where(report => report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                (report.Status == DailyReportStatus.Approved || report.Status == DailyReportStatus.Superseded) &&
                report.ReviewedAt.HasValue &&
                report.ReviewedAt.Value <= cutoff)
            .Select(report => new
            {
                ApprovedAt = report.ReviewedAt!.Value,
                report.LastModifiedAt,
                report.SupersededAt
            })
            .ToArrayAsync(cancellationToken);

        return versions
            .SelectMany(version => new DateTimeOffset?[]
            {
                version.ApprovedAt,
                version.LastModifiedAt <= cutoff ? version.LastModifiedAt : null,
                version.SupersededAt <= cutoff ? version.SupersededAt : null
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value.ToUniversalTime())
            .OrderByDescending(value => value)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLatestApprovedChangesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, DateTimeOffset>();
        }

        var ids = projectIds.Distinct().ToArray();
        var changes = await dbContext.DailyReports
            .AsNoTracking()
            .Where(report => report.TenantId == tenantId &&
                ids.Contains(report.ProjectId) &&
                report.Status == DailyReportStatus.Approved)
            .Select(report => new { report.ProjectId, report.LastModifiedAt })
            .ToArrayAsync(cancellationToken);

        return changes
            .GroupBy(item => item.ProjectId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.LastModifiedAt));
    }

    private static ApprovedDailyReportRecord Map(DailyReport report) => new(
        report.Id,
        report.ReportDate,
        report.ReviewedAt ?? report.LastModifiedAt,
        report.Facts.Select(fact => new ApprovedDailyFactRecord(
            fact.Id,
            Map(fact.Kind),
            fact.Description,
            fact.Category,
            fact.LocationName,
            fact.Quantity,
            fact.Unit,
            fact.ResourceCount,
            fact.Hours,
            Map(fact.ImpactLevel),
            fact.ReferenceCode,
            fact.MeasurementItemId,
            fact.LocationId)).ToArray());

    private static ApprovedDailyFactKind Map(DailyFactKind kind) => kind switch
    {
        DailyFactKind.WorkProgress => ApprovedDailyFactKind.WorkProgress,
        DailyFactKind.Labor => ApprovedDailyFactKind.Labor,
        DailyFactKind.Equipment => ApprovedDailyFactKind.Equipment,
        DailyFactKind.Material => ApprovedDailyFactKind.Material,
        DailyFactKind.Issue => ApprovedDailyFactKind.Issue,
        DailyFactKind.Stoppage => ApprovedDailyFactKind.Stoppage,
        DailyFactKind.SiteCondition => ApprovedDailyFactKind.SiteCondition,
        DailyFactKind.Note => ApprovedDailyFactKind.Note,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported approved fact kind.")
    };

    private static ApprovedDailyImpactLevel? Map(DailyImpactLevel? impactLevel) => impactLevel switch
    {
        null => null,
        DailyImpactLevel.Low => ApprovedDailyImpactLevel.Low,
        DailyImpactLevel.Medium => ApprovedDailyImpactLevel.Medium,
        DailyImpactLevel.High => ApprovedDailyImpactLevel.High,
        DailyImpactLevel.Critical => ApprovedDailyImpactLevel.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(impactLevel), impactLevel, "Unsupported impact level.")
    };
}
