using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Persistence;

namespace Pmcs.Modules.ProjectIntelligence.Services;

internal sealed class ProjectStateContextSource(ProjectIntelligenceDbContext dbContext) : IProjectStateContextSource
{
    public async Task<ProjectStateContextRecord?> GetLatestAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.ProjectStateSnapshots
            .AsNoTracking()
            .Include(item => item.AttentionItems)
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.CalculatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        return new ProjectStateContextRecord(
            snapshot.Id,
            snapshot.ProjectId,
            snapshot.ProjectCode,
            snapshot.ProjectName,
            snapshot.CalculationVersion,
            snapshot.ProjectConfigurationRevision,
            snapshot.AsOfDate,
            snapshot.CalculatedAt,
            snapshot.AssessmentScope.ToString(),
            snapshot.IsPartial,
            snapshot.OperationalStatus.ToString(),
            snapshot.CoverageStatus.ToString(),
            snapshot.FreshnessStatus.ToString(),
            snapshot.ConfidenceStatus.ToString(),
            snapshot.CoveragePercent,
            snapshot.ExpectedReportDays,
            snapshot.ApprovedReportDays,
            snapshot.LastApprovedReportDate,
            snapshot.ApprovedFactCount,
            snapshot.ProgressFactCount,
            snapshot.LaborFactCount,
            snapshot.EquipmentFactCount,
            snapshot.MaterialFactCount,
            snapshot.IssueCount,
            snapshot.StoppageCount,
            snapshot.HighImpactCount,
            snapshot.CriticalImpactCount,
            snapshot.OldestAttentionAgeDays,
            snapshot.ContractState,
            snapshot.PlanningState,
            snapshot.BudgetState,
            snapshot.QualityState,
            snapshot.HseState,
            snapshot.SourceMaxChangedAt,
            snapshot.AttentionItems
                .OrderByDescending(item => item.Priority)
                .ThenByDescending(item => item.AgeDays)
                .Select(item => new ProjectStateAttentionContextRecord(
                    item.SourceReportId,
                    item.SourceFactId,
                    item.ReportDate,
                    item.Kind.ToString(),
                    item.Description,
                    item.Category,
                    item.LocationName,
                    item.ObservedImpact?.ToString(),
                    item.Priority.ToString(),
                    item.AgeDays,
                    item.Status.ToString(),
                    item.ReferenceCode))
                .ToArray());
    }
}
