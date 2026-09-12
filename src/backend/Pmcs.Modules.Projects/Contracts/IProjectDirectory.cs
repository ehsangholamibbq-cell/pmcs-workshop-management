using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Contracts;

public interface IProjectDirectory
{
    Task<bool> ExistsAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectControlProfile?> FindProfileAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProjectControlProfile>> ListProfilesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid>? projectIds = null,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectControlProfile(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    string BaseCurrencyCode,
    long Revision,
    DateTimeOffset? ConfigurationChangedAt,
    ProjectStatus Status,
    ContractModel ContractModel,
    PlanningMode PlanningMode,
    ProjectFeatureState Contract,
    ProjectFeatureState Planning,
    ProjectFeatureState Budget,
    ProjectFeatureState Quality,
    ProjectFeatureState Hse,
    ProjectFeatureState Finance,
    ProjectFeatureState Procurement,
    ProjectCalendarProfile Calendar);

public sealed record ProjectCalendarProfile(
    ProjectCalendarConfigurationState State,
    int? WorkingDaysMask)
{
    public bool IsWorkingDay(DayOfWeek dayOfWeek) =>
        State == ProjectCalendarConfigurationState.Configured &&
        WorkingDaysMask.HasValue &&
        (WorkingDaysMask.Value & (1 << (int)dayOfWeek)) != 0;
}

public enum ProjectCalendarConfigurationState
{
    NotConfigured = 1,
    Configured = 2
}

public enum ProjectFeatureState
{
    NotConfigured = 1,
    NotEnabled = 2,
    SetupRequired = 3,
    Active = 4,
    Suspended = 5
}
