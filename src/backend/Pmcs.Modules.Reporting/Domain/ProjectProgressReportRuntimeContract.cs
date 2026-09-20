using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public static class ProjectProgressReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F04-SEMANTIC-001";
    public const string DefinitionCode = "project-progress-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion = "pmcs.reporting.project-progress.parameters/v1";
    public const string SnapshotSchemaVersion = "pmcs.reporting.project-progress.snapshot/v1";
    public const string TemplateVersion = "1.0.0";
    public const string TemplateContentDigest =
        "3f19d880a7790854fcc0d79d4822c5653cb6bb888294eadf8eaeeee8b5857816";
    public const string RendererContractVersion = "pmcs.reporting.project-progress.renderer/v1";
    public const string LayoutContractVersion = "pmcs.reporting.project-progress.layout/v1";
    public const string PinnedProjectProfileSchemaVersion =
        "pmcs.reporting.project-progress.project-profile/v1";
}

public sealed record ProjectProgressReportParameters;

public sealed record ProjectProgressPinnedProjectProfile(
    string SchemaVersion,
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt,
    ProjectStatus Status)
{
    public static ProjectProgressPinnedProjectProfile Capture(ProjectControlProfile project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.ConfigurationChangedAt.HasValue)
        {
            throw new DomainRuleException(
                "reporting.project_progress.project_configuration.unversioned",
                "The Project profile must be versioned before it can be pinned.");
        }

        return new ProjectProgressPinnedProjectProfile(
            ProjectProgressReportRuntimeContract.PinnedProjectProfileSchemaVersion,
            project.Id,
            project.TenantId,
            project.Code,
            project.Name,
            project.TimeZone,
            project.Revision,
            project.ConfigurationVersion,
            project.ConfigurationChangedAt.Value.ToUniversalTime(),
            project.Status);
    }

    public TimeZoneInfo ValidateForRun(
        Guid tenantId,
        Guid projectId,
        DateTimeOffset sourceCutoffUtc,
        DateTimeOffset acceptedAtUtc)
    {
        var cutoff = sourceCutoffUtc.ToUniversalTime();
        var acceptedAt = acceptedAtUtc.ToUniversalTime();
        if (!string.Equals(
                SchemaVersion,
                ProjectProgressReportRuntimeContract.PinnedProjectProfileSchemaVersion,
                StringComparison.Ordinal) ||
            Id == Guid.Empty || TenantId == Guid.Empty || Id != projectId || TenantId != tenantId ||
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(TimeZone) || Revision <= 0 || ConfigurationVersion <= 0 ||
            ConfigurationChangedAt == default || ConfigurationChangedAt.ToUniversalTime() > acceptedAt ||
            cutoff == default || cutoff > acceptedAt || Status != ProjectStatus.Active)
        {
            throw new DomainRuleException(
                "reporting.project_progress.project_scope.invalid",
                "The pinned Project profile does not match the accepted progress report run.");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw InvalidTimeZone();
        }
        catch (InvalidTimeZoneException)
        {
            throw InvalidTimeZone();
        }
    }

    private static DomainRuleException InvalidTimeZone() => new(
        "reporting.project_progress.time_zone.invalid",
        "The pinned Project time zone is unknown or invalid.");
}

public enum ProjectProgressReportReasonCode
{
    ProgressReportingNotConfigured = 1,
    PlanningModeNone = 2,
    OfficialBaselineMissing = 3,
    PlanningModeBaselineMismatch = 4,
    OfficialActualMissing = 5,
    OfficialActualIncomplete = 6,
    ScheduleNotConfiguredForMeasurementWeights = 7,
    CalendarDaysFallback = 8,
    ApprovedProgressOutsideBaseline = 9
}
