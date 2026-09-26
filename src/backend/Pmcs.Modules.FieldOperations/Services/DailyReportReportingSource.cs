using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Persistence;

namespace Pmcs.Modules.FieldOperations.Services;

internal sealed class DailyReportReportingSource(FieldOperationsDbContext dbContext)
    : IDailyReportReportingSource
{
    public async Task<DailyReportReportingChain?> LoadChainAsync(
        Guid tenantId,
        Guid projectId,
        Guid reportId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || projectId == Guid.Empty || reportId == Guid.Empty)
        {
            return null;
        }

        var normalizedAsOf = asOfUtc.ToUniversalTime();
        var rootReportId = await dbContext.DailyReports
            .AsNoTracking()
            .Where(report => report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                report.Id == reportId)
            .Select(report => (Guid?)report.RootReportId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!rootReportId.HasValue)
        {
            return null;
        }

        var reports = await dbContext.DailyReports
            .AsNoTracking()
            .Include(report => report.Facts)
            .Where(report => report.TenantId == tenantId &&
                report.ProjectId == projectId &&
                report.RootReportId == rootReportId.Value &&
                (report.Status == DailyReportStatus.Approved || report.Status == DailyReportStatus.Superseded) &&
                report.ReviewedAt.HasValue &&
                report.ReviewedAt.Value <= normalizedAsOf)
            .OrderBy(report => report.VersionNumber)
            .ThenBy(report => report.Id)
            .ToListAsync(cancellationToken);

        var versions = reports.Select(report => MapForCutoff(report, normalizedAsOf)).ToArray();
        var current = versions
            .Where(version => DailyReportReportingRules.IsOfficialAt(
                version.ApprovedAt,
                version.SupersededAt,
                normalizedAsOf))
            .OrderByDescending(version => version.VersionNumber)
            .ThenByDescending(version => version.ReportId)
            .ToArray();

        return new DailyReportReportingChain(
            rootReportId.Value,
            current.Length == 1 ? current[0].ReportId : null,
            normalizedAsOf,
            versions);
    }

    internal static DailyReportReportingVersion MapForCutoff(
        DailyReport report,
        DateTimeOffset asOfUtc)
    {
        var approvedAt = report.ReviewedAt
            ?? throw new InvalidOperationException("An official report requires approval time.");
        var normalizedAsOf = asOfUtc.ToUniversalTime();
        var actualSupersededAt = report.SupersededAt;
        var supersededAt = actualSupersededAt is { } supersessionTime &&
            supersessionTime <= normalizedAsOf
                ? supersessionTime
                : (DateTimeOffset?)null;
        var isSupersededAtCutoff = supersededAt.HasValue;
        var hasFutureSupersession = actualSupersededAt.HasValue && !isSupersededAtCutoff;
        var lastModifiedAt = report.LastModifiedAt <= normalizedAsOf
            ? report.LastModifiedAt
            : approvedAt;
        var revision = hasFutureSupersession
            ? checked(report.Revision - 1)
            : report.Revision;

        return new DailyReportReportingVersion(
            report.Id,
            report.RootReportId,
            report.VersionNumber,
            report.SupersedesReportId,
            isSupersededAtCutoff ? report.SupersededByReportId : null,
            supersededAt,
            report.ReportDate,
            report.LocationName,
            report.Narrative,
            isSupersededAtCutoff
                ? DailyReportReportingVersionState.Superseded
                : DailyReportReportingVersionState.Approved,
            report.CreatedBy,
            report.CreatedAt,
            report.ReviewedBy,
            approvedAt,
            lastModifiedAt,
            report.SupersedesReportId.HasValue || isSupersededAtCutoff
                ? report.CorrectionReason
                : null,
            report.CorrectionInitiatedBy,
            revision,
            report.Facts
                .OrderBy(fact => fact.Id)
                .Select(Map)
                .ToArray());
    }

    private static DailyReportReportingFact Map(DailyReportFact fact) => new(
        fact.Id,
        fact.CopiedFromFactId,
        Map(fact.Kind),
        fact.Description,
        fact.Category,
        fact.LocationId,
        fact.LocationName,
        fact.Quantity,
        fact.Unit,
        fact.ResourceCount,
        fact.Hours,
        Map(fact.ImpactLevel),
        fact.ReferenceCode,
        fact.MeasurementItemId,
        fact.CreatedBy,
        fact.CreatedAt);

    private static DailyReportReportingFactKind Map(DailyFactKind kind) => kind switch
    {
        DailyFactKind.WorkProgress => DailyReportReportingFactKind.WorkProgress,
        DailyFactKind.Labor => DailyReportReportingFactKind.Labor,
        DailyFactKind.Equipment => DailyReportReportingFactKind.Equipment,
        DailyFactKind.Material => DailyReportReportingFactKind.Material,
        DailyFactKind.Issue => DailyReportReportingFactKind.Issue,
        DailyFactKind.Stoppage => DailyReportReportingFactKind.Stoppage,
        DailyFactKind.SiteCondition => DailyReportReportingFactKind.SiteCondition,
        DailyFactKind.Note => DailyReportReportingFactKind.Note,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported fact kind.")
    };

    private static DailyReportReportingImpactLevel? Map(DailyImpactLevel? impactLevel) => impactLevel switch
    {
        null => null,
        DailyImpactLevel.Low => DailyReportReportingImpactLevel.Low,
        DailyImpactLevel.Medium => DailyReportReportingImpactLevel.Medium,
        DailyImpactLevel.High => DailyReportReportingImpactLevel.High,
        DailyImpactLevel.Critical => DailyReportReportingImpactLevel.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(impactLevel), impactLevel, "Unsupported impact level.")
    };
}
