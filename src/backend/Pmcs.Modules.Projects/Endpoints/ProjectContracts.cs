using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Projects.Endpoints;

public sealed record CreateProjectRequest(
    string Code,
    string Name,
    ContractModel ContractModel,
    PlanningMode PlanningMode,
    CapabilityMode BudgetMode,
    CapabilityMode QualityMode,
    CapabilityMode HseMode,
    string TimeZone,
    CapabilityMode? FinanceMode = null,
    string? BaseCurrencyCode = null,
    CapabilityMode? ProcurementMode = null);

public sealed record ConfigureProjectCalendarRequest(
    long BaseRevision,
    ProjectCalendarMode Mode,
    IReadOnlyCollection<DayOfWeek>? WorkingDays);

public sealed record ConfigureProjectPlanningModeRequest(
    long BaseRevision,
    PlanningMode Mode);

public sealed record ActivateProjectRequest(long BaseRevision);

public sealed record ProjectResponse(
    Guid Id,
    string Code,
    string Name,
    ContractModel ContractModel,
    PlanningMode PlanningMode,
    CapabilityMode BudgetMode,
    CapabilityMode QualityMode,
    CapabilityMode HseMode,
    CapabilityMode FinanceMode,
    CapabilityMode ProcurementMode,
    string BaseCurrencyCode,
    ProjectCalendarMode CalendarMode,
    IReadOnlyCollection<DayOfWeek> WorkingDays,
    DateTimeOffset? ConfigurationChangedAt,
    string TimeZone,
    ProjectStatus Status,
    Guid? ActivatedBy,
    DateTimeOffset? ActivatedAt,
    long Revision)
{
    public static ProjectResponse From(Project project) => new(
        project.Id,
        project.Code,
        project.Name,
        project.ContractModel,
        project.PlanningMode,
        project.BudgetMode,
        project.QualityMode,
        project.HseMode,
        project.FinanceMode,
        project.ProcurementMode,
        project.BaseCurrencyCode,
        project.CalendarMode,
        ProjectCalendarMask.ToDays(project.WorkingDaysMask),
        project.ConfigurationChangedAt,
        project.TimeZone,
        project.Status,
        project.ActivatedBy,
        project.ActivatedAt,
        project.Revision);
}

internal static class ProjectCalendarMask
{
    public static int? FromDays(ProjectCalendarMode mode, IReadOnlyCollection<DayOfWeek>? days)
    {
        if (mode == ProjectCalendarMode.NotConfigured)
        {
            return days is null or { Count: 0 } ? null : days.Aggregate(0, (mask, day) => mask | (1 << (int)day));
        }

        return days?.Distinct().Aggregate(0, (mask, day) => mask | (1 << (int)day));
    }

    public static IReadOnlyCollection<DayOfWeek> ToDays(int? mask) =>
        mask.HasValue
            ? Enum.GetValues<DayOfWeek>().Where(day => (mask.Value & (1 << (int)day)) != 0).ToArray()
            : Array.Empty<DayOfWeek>();
}
