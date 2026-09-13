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
    CapabilityMode? ProcurementMode = null,
    ProjectType ProjectType = ProjectType.NotConfigured,
    ProjectExecutionPhase ExecutionPhase = ProjectExecutionPhase.NotConfigured,
    string? CountryCode = null,
    string? Region = null,
    DateOnly? StartDate = null,
    DateOnly? PlannedFinishDate = null,
    string? ShortDescription = null,
    ProjectUnitSystem UnitSystem = ProjectUnitSystem.NotConfigured,
    TimeOnly? DailyCutoffLocalTime = null,
    ReportingFrequency ReportingFrequency = ReportingFrequency.NotConfigured,
    DailyReportWorkflow DailyReportWorkflow = DailyReportWorkflow.NotConfigured,
    bool OfflinePolicyAccepted = false,
    ProjectCalendarMode CalendarMode = ProjectCalendarMode.NotConfigured,
    IReadOnlyCollection<DayOfWeek>? WorkingDays = null);

public sealed record ConfigureProjectSetupRequest(
    long BaseRevision,
    ContractModel ContractModel,
    PlanningMode PlanningMode,
    CapabilityMode BudgetMode,
    CapabilityMode QualityMode,
    CapabilityMode HseMode,
    CapabilityMode FinanceMode,
    CapabilityMode ProcurementMode,
    ProjectCalendarMode CalendarMode,
    IReadOnlyCollection<DayOfWeek>? WorkingDays,
    ProjectType ProjectType,
    ProjectExecutionPhase ExecutionPhase,
    string CountryCode,
    string Region,
    DateOnly? StartDate,
    DateOnly? PlannedFinishDate,
    string ShortDescription,
    string TimeZone,
    string BaseCurrencyCode,
    ProjectUnitSystem UnitSystem,
    TimeOnly? DailyCutoffLocalTime,
    ReportingFrequency ReportingFrequency,
    DailyReportWorkflow DailyReportWorkflow,
    bool OfflinePolicyAccepted,
    string? Reason);

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
    ProjectType ProjectType,
    ProjectExecutionPhase ExecutionPhase,
    string CountryCode,
    string Region,
    DateOnly? StartDate,
    DateOnly? PlannedFinishDate,
    string ShortDescription,
    ProjectUnitSystem UnitSystem,
    TimeOnly? DailyCutoffLocalTime,
    ReportingFrequency ReportingFrequency,
    DailyReportWorkflow DailyReportWorkflow,
    bool OfflinePolicyAccepted,
    long ConfigurationVersion,
    long? ActivatedConfigurationVersion,
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
        project.ProjectType,
        project.ExecutionPhase,
        project.CountryCode,
        project.Region,
        project.StartDate,
        project.PlannedFinishDate,
        project.ShortDescription,
        project.UnitSystem,
        project.DailyCutoffLocalTime,
        project.ReportingFrequency,
        project.DailyReportWorkflow,
        project.OfflinePolicyAccepted,
        project.ConfigurationVersion,
        project.ActivatedConfigurationVersion,
        project.CalendarMode,
        ProjectCalendarMask.ToDays(project.WorkingDaysMask),
        project.ConfigurationChangedAt,
        project.TimeZone,
        project.Status,
        project.ActivatedBy,
        project.ActivatedAt,
        project.Revision);
}

public sealed record ProjectReadinessItem(
    string Code,
    string Title,
    ProjectReadinessStatus Status,
    string Detail);

public sealed record ProjectReadinessResponse(
    Guid ProjectId,
    long ConfigurationVersion,
    bool IsReady,
    int CompletionPercent,
    IReadOnlyCollection<ProjectReadinessItem> Items);

public enum ProjectReadinessStatus
{
    Passed = 1,
    Warning = 2,
    Blocked = 3
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
