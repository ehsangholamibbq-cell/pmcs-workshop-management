using Microsoft.EntityFrameworkCore;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class DailyReportPeriodReportingSource(FieldOperationsDbContext dbContext)
    : IDailyReportPeriodReportingSource
{
    public async Task<DailyReportReportingPeriod> LoadPeriodAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly periodStartLocalDate,
        DateOnly periodEndLocalDateExclusive,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainRuleException(
                "field.daily_report.period.identity.required",
                "Tenant and project ids are required for a period reporting source.");
        }

        if (periodEndLocalDateExclusive <= periodStartLocalDate)
        {
            throw new DomainRuleException(
                "field.daily_report.period.range.invalid",
                "The reporting period must be a non-empty half-open date range.");
        }

        var normalizedAsOf = asOfUtc.ToUniversalTime();
        var reports = await dbContext.DailyReports
            .AsNoTracking()
            .Include(report => report.Facts)
            .Where(report => report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                report.ReportDate >= periodStartLocalDate &&
                report.ReportDate < periodEndLocalDateExclusive &&
                report.CreatedAt <= normalizedAsOf)
            .OrderBy(report => report.ReportDate)
            .ThenBy(report => report.RootReportId)
            .ThenBy(report => report.VersionNumber)
            .ThenBy(report => report.Id)
            .ToListAsync(cancellationToken);

        var roots = reports
            .GroupBy(report => report.RootReportId)
            .Select(group => MapRoot(group.Key, group.ToArray(), normalizedAsOf))
            .OfType<DailyReportReportingRoot>()
            .OrderBy(root => root.ReportDate)
            .ThenBy(root => root.RootReportId)
            .ToArray();

        var duplicateDate = roots
            .GroupBy(root => root.ReportDate)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDate is not null)
        {
            throw new DomainRuleException(
                "field.daily_report.period.duplicate_root_date",
                "More than one daily-report root exists for the same project date.");
        }

        return new DailyReportReportingPeriod(
            DailyReportPeriodReportingContract.Version,
            tenantId,
            projectId,
            periodStartLocalDate,
            periodEndLocalDateExclusive,
            normalizedAsOf,
            roots);
    }

    private static DailyReportReportingRoot? MapRoot(
        Guid rootReportId,
        IReadOnlyCollection<DailyReport> reports,
        DateTimeOffset asOfUtc)
    {
        var dates = reports.Select(report => report.ReportDate).Distinct().ToArray();
        if (dates.Length != 1)
        {
            throw new DomainRuleException(
                "field.daily_report.period.root_date_conflict",
                "A daily-report root cannot span multiple report dates.");
        }

        var versions = reports
            .Where(report =>
                (report.Status == DailyReportStatus.Approved || report.Status == DailyReportStatus.Superseded) &&
                report.ReviewedAt.HasValue &&
                report.ReviewedAt.Value <= asOfUtc)
            .Select(report => DailyReportReportingSource.MapForCutoff(report, asOfUtc))
            .Select(Canonicalize)
            .OrderBy(version => version.VersionNumber)
            .ThenBy(version => version.ReportId)
            .ToArray();
        if (versions.Length == 0)
        {
            // Draft/submitted/returned/rejected roots are not part of the reporting contract.
            return null;
        }

        var current = versions
            .Where(version => DailyReportReportingRules.IsOfficialAt(
                version.ApprovedAt,
                version.SupersededAt,
                asOfUtc))
            .ToArray();
        if (current.Length > 1)
        {
            throw new DomainRuleException(
                "field.daily_report.period.duplicate_current_official",
                "More than one current official version exists for a daily-report root.");
        }

        return new DailyReportReportingRoot(
            rootReportId,
            dates[0],
            current.SingleOrDefault()?.ReportId,
            DailyReportReportingClassification.Internal,
            versions);
    }

    private static DailyReportReportingVersion Canonicalize(DailyReportReportingVersion version) =>
        version with
        {
            Facts = version.Facts
                .OrderBy(fact => fact.Kind)
                .ThenBy(fact => fact.FactId)
                .ToArray()
        };
}
