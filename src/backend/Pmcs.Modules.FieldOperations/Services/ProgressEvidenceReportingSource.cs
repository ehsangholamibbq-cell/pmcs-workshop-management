using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class ProgressEvidenceReportingSource(FieldOperationsDbContext dbContext)
    : IProgressEvidenceReportingSource
{
    public async Task<ProgressEvidenceReportingProjection> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly throughLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var reports = await dbContext.DailyReports
            .AsNoTracking()
            .Include(report => report.Facts)
            .Where(report => report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                report.ReportDate <= throughLocalDate &&
                (report.Status == DailyReportStatus.Approved ||
                    report.Status == DailyReportStatus.Superseded) &&
                report.ReviewedAt.HasValue &&
                report.ReviewedAt.Value <= cutoff)
            .OrderBy(report => report.ReportDate)
            .ThenBy(report => report.RootReportId)
            .ThenBy(report => report.VersionNumber)
            .ThenBy(report => report.Id)
            .ToArrayAsync(cancellationToken);

        return ProgressEvidenceReportingSelector.Select(
            tenantId,
            projectId,
            throughLocalDate,
            cutoff,
            reports.Select(Map).ToArray());
    }

    private static ProgressEvidenceReportingVersion Map(DailyReport report)
    {
        var approvedAt = report.ReviewedAt
            ?? throw new InvalidOperationException("An official daily report requires approval time.");
        return new ProgressEvidenceReportingVersion(
            report.Id,
            report.RootReportId,
            report.VersionNumber,
            report.ReportDate,
            approvedAt,
            report.SupersededAt,
            report.Facts
                .Where(fact => fact.Kind == DailyFactKind.WorkProgress)
                .Select(fact => new ProgressEvidenceReportingFact(
                    fact.Id,
                    fact.MeasurementItemId,
                    fact.Quantity,
                    fact.Unit,
                    fact.CreatedAt))
                .ToArray());
    }
}
