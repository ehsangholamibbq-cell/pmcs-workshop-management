using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class ProgressFactSource(FieldOperationsDbContext dbContext) : IProgressFactSource
{
    public async Task<IReadOnlyCollection<ProgressFactRecord>> LoadAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from fact in dbContext.DailyReportFacts.AsNoTracking()
            join report in dbContext.DailyReports.AsNoTracking()
                on fact.DailyReportId equals report.Id
            where report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                (report.Status == DailyReportStatus.Submitted || report.Status == DailyReportStatus.Approved) &&
                fact.Kind == DailyFactKind.WorkProgress
            orderby report.ReportDate
            select new ProgressFactRecord(
                    fact.Id,
                    report.Id,
                    report.ReportDate,
                    report.Status == DailyReportStatus.Approved
                        ? ProgressFactReviewState.Approved
                        : ProgressFactReviewState.Provisional,
                    fact.MeasurementItemId,
                    fact.Category,
                    fact.Quantity,
                    fact.Unit,
                    report.LastModifiedAt))
            .ToListAsync(cancellationToken);
    }
}
