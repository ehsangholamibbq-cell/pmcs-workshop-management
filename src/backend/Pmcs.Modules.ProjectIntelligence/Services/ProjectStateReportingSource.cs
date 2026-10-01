using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Persistence;

namespace Pmcs.Modules.ProjectIntelligence.Services;

internal sealed class ProjectStateReportingSource(
    ProjectIntelligenceDbContext dbContext,
    IApprovedDailyFactSource approvedDailyFactSource) : IProjectStateReportingSource
{
    public async Task<ProjectStateReportingSelection> LoadAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var snapshots = await dbContext.ProjectStateSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.AttentionItems)
            .Where(snapshot => snapshot.TenantId == tenantId &&
                snapshot.ProjectId == projectId &&
                snapshot.CalculatedAt <= cutoff &&
                snapshot.AsOfDate <= cutoffLocalDate)
            .ToArrayAsync(cancellationToken);
        var latestApprovedSourceChangedAt = await approvedDailyFactSource.GetLatestApprovedChangeAtAsync(
            tenantId,
            projectId,
            cutoff,
            cancellationToken);

        return ProjectStateReportingSelector.Select(
            tenantId,
            projectId,
            cutoffLocalDate,
            cutoff,
            ProjectStateReportingSourceState.Configured,
            ProjectStateReportingClassification.Internal,
            latestApprovedSourceChangedAt,
            snapshots.Select(Map).ToArray());
    }

    private static ProjectStateReportingSnapshot Map(ProjectStateSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.TenantId,
        snapshot.ProjectId,
        snapshot.ProjectCode,
        snapshot.ProjectName,
        ProjectStateReportingClassification.Internal,
        snapshot.CalculationVersion,
        snapshot.ProjectConfigurationRevision,
        snapshot.AsOfDate,
        snapshot.CalculatedAt,
        snapshot.WindowStart,
        snapshot.WindowEnd,
        snapshot.AssessmentScope,
        snapshot.IsPartial,
        snapshot.OperationalStatus,
        snapshot.CoverageStatus,
        snapshot.FreshnessStatus,
        snapshot.ConfidenceStatus,
        snapshot.CoverageBasis,
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
        snapshot.AttentionItems.Select(item => new ProjectStateReportingAttentionItem(
            item.SourceReportId,
            item.SourceFactId,
            item.ReportDate,
            item.Kind,
            item.Description,
            item.Category,
            item.LocationId,
            item.LocationName,
            item.ObservedImpact,
            item.Priority,
            item.AgeDays,
            item.AgeBand,
            item.Status,
            item.ReferenceCode)).ToArray());
}
