using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public static class ProjectPeriodicReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F02-SEMANTIC-001";
    public const string DefinitionCode = "project-periodic-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion = "pmcs.reporting.project-periodic.parameters/v1";
    public const string SnapshotSchemaVersion = "pmcs.reporting.project-periodic.snapshot/v1";
    public const string TemplateVersion = "1.0.0";
    public const string TemplateContentDigest =
        "eca353e00f07fbdb054613768399496e137ab0deda731c5d319d5cd4ebd3be6b";
    public const string RendererContractVersion = "pmcs.reporting.project-periodic.renderer/v1";
    public const string LayoutContractVersion = "pmcs.reporting.project-periodic.layout/v1";
    public const string PinnedProjectProfileSchemaVersion =
        "pmcs.reporting.project-periodic.project-profile/v1";
}

public sealed record ProjectPeriodicReportParameters(
    ProjectReportPeriodKind PeriodKind,
    DateOnly PeriodStartLocalDate);

public sealed record ProjectPeriodicPinnedProjectProfile(
    string SchemaVersion,
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt,
    ProjectStatus Status,
    ProjectCalendarConfigurationState CalendarState,
    int? WorkingDaysMask,
    TimeOnly? DailyCutoffLocalTime,
    ReportingFrequency ReportingFrequency,
    DailyReportWorkflow DailyReportWorkflow)
{
    public static ProjectPeriodicPinnedProjectProfile Capture(ProjectControlProfile project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.ConfigurationChangedAt.HasValue)
        {
            throw new DomainRuleException(
                "reporting.period.project_configuration.unversioned",
                "The project reporting configuration must be versioned before it can be pinned.");
        }

        return new ProjectPeriodicPinnedProjectProfile(
            ProjectPeriodicReportRuntimeContract.PinnedProjectProfileSchemaVersion,
            project.Id,
            project.TenantId,
            project.Code,
            project.Name,
            project.TimeZone,
            project.Revision,
            project.ConfigurationVersion,
            project.ConfigurationChangedAt.Value.ToUniversalTime(),
            project.Status,
            project.Calendar.State,
            project.Calendar.WorkingDaysMask,
            project.DailyCutoffLocalTime,
            project.ReportingFrequency,
            project.DailyReportWorkflow);
    }

    public void ValidateForRun(
        Guid tenantId,
        Guid projectId,
        string projectTimeZone,
        DateTimeOffset acceptedAtUtc)
    {
        if (!string.Equals(
                SchemaVersion,
                ProjectPeriodicReportRuntimeContract.PinnedProjectProfileSchemaVersion,
                StringComparison.Ordinal) ||
            Id == Guid.Empty || TenantId == Guid.Empty || Id != projectId || TenantId != tenantId ||
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(TimeZone) ||
            !string.Equals(TimeZone, projectTimeZone, StringComparison.Ordinal) ||
            Revision <= 0 || ConfigurationVersion <= 0 ||
            ConfigurationChangedAt == default ||
            ConfigurationChangedAt.ToUniversalTime() > acceptedAtUtc.ToUniversalTime() ||
            Status != ProjectStatus.Active ||
            !Enum.IsDefined(CalendarState) || !Enum.IsDefined(ReportingFrequency) ||
            !Enum.IsDefined(DailyReportWorkflow) ||
            (WorkingDaysMask.HasValue && WorkingDaysMask is not (>= 1 and <= 127)))
        {
            throw new DomainRuleException(
                "reporting.period.project_scope.invalid",
                "The pinned project reporting profile does not match the accepted run.");
        }
    }

    public bool IsWorkingDay(DayOfWeek dayOfWeek) =>
        CalendarState == ProjectCalendarConfigurationState.Configured &&
        WorkingDaysMask.HasValue &&
        (WorkingDaysMask.Value & (1 << (int)dayOfWeek)) != 0;
}

public enum ProjectReportPeriodKind
{
    Weekly = 1,
    Monthly = 2
}

public enum ProjectPeriodicReportReasonCode
{
    ReportingCadenceMissing = 1,
    DailyWorkflowMissing = 2,
    DailyCutoffMissing = 3,
    WorkingCalendarMissing = 4,
    PeriodOpenAtCutoff = 5,
    ExpectedSlotMissing = 6,
    OfficialVersionMissing = 7,
    OfficialReportEmpty = 8
}

public enum ProjectPeriodicReportUnitState
{
    SourceUnit = 1,
    UnitMissing = 2
}
