using Microsoft.EntityFrameworkCore;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Persistence;

internal sealed class ProjectDirectory(ProjectsDbContext dbContext) : IProjectDirectory
{
    public Task<bool> ExistsAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default) =>
        dbContext.Projects.AsNoTracking().AnyAsync(
            project => project.TenantId == tenantId && project.Id == projectId,
            cancellationToken);

    public async Task<ProjectControlProfile?> FindProfileAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantId == tenantId && item.Id == projectId,
                cancellationToken);

        return project is null ? null : Map(project);
    }

    public async Task<IReadOnlyCollection<ProjectControlProfile>> ListProfilesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid>? projectIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Projects.AsNoTracking().Where(project => project.TenantId == tenantId);
        if (projectIds is not null)
        {
            var ids = projectIds.ToArray();
            query = query.Where(project => ids.Contains(project.Id));
        }

        var projects = await query.OrderBy(project => project.Code).ToListAsync(cancellationToken);
        return projects.Select(Map).ToArray();
    }

    private static ProjectControlProfile Map(Project project) => new(
        project.Id,
        project.TenantId,
        project.Code,
        project.Name,
        project.TimeZone,
        project.BaseCurrencyCode,
        project.Revision,
        project.ConfigurationChangedAt,
        project.Status,
        project.ContractModel,
        project.PlanningMode,
        project.ContractModel == ContractModel.NotConfigured
            ? ProjectFeatureState.NotConfigured
            : ProjectFeatureState.Active,
        project.PlanningMode == PlanningMode.None
            ? ProjectFeatureState.NotConfigured
            : ProjectFeatureState.Active,
        Map(project.BudgetMode),
        Map(project.QualityMode),
        Map(project.HseMode),
        Map(project.FinanceMode),
        Map(project.ProcurementMode),
        new ProjectCalendarProfile(
            project.CalendarMode == ProjectCalendarMode.NotConfigured
                ? ProjectCalendarConfigurationState.NotConfigured
                : ProjectCalendarConfigurationState.Configured,
            project.WorkingDaysMask),
        project.ConfigurationVersion,
        project.DailyCutoffLocalTime,
        project.ReportingFrequency,
        project.DailyReportWorkflow);

    private static ProjectFeatureState Map(CapabilityMode mode) => mode switch
    {
        CapabilityMode.NotEnabled => ProjectFeatureState.NotEnabled,
        CapabilityMode.SetupRequired => ProjectFeatureState.SetupRequired,
        CapabilityMode.Active => ProjectFeatureState.Active,
        CapabilityMode.Suspended => ProjectFeatureState.Suspended,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported project capability mode.")
    };
}
