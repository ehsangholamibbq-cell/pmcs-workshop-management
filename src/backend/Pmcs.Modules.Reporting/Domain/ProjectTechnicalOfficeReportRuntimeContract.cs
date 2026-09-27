using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public static class ProjectTechnicalOfficeReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F07-SEMANTIC-001";
    public const string DefinitionCode = "project-technical-office-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion = "pmcs.reporting.project-technical-office.parameters/v1";
    public const string SnapshotSchemaVersion = "pmcs.reporting.project-technical-office.snapshot/v1";
    public const string PinnedProjectProfileSchemaVersion = "pmcs.reporting.project-technical-office.project-profile/v1";
    public static readonly IReadOnlyCollection<string> RequiredSourcePermissions =
        ["technical.read", "technical.confidential.read"];
}

public sealed record ProjectTechnicalOfficeReportParameters;

public sealed record ProjectTechnicalOfficePinnedProjectProfile(
    string SchemaVersion, Guid Id, Guid TenantId, string Code, string Name,
    string TimeZone, long Revision, long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAtUtc, DateTimeOffset CapturedAtUtc,
    ProjectStatus Status)
{
    public static ProjectTechnicalOfficePinnedProjectProfile Capture(
        ProjectControlProfile project, DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.ConfigurationChangedAt.HasValue)
            throw Invalid("configuration.unversioned", "Project configuration must be versioned.");
        return new ProjectTechnicalOfficePinnedProjectProfile(
            ProjectTechnicalOfficeReportRuntimeContract.PinnedProjectProfileSchemaVersion,
            project.Id, project.TenantId, project.Code, project.Name, project.TimeZone,
            project.Revision, project.ConfigurationVersion,
            project.ConfigurationChangedAt.Value.ToUniversalTime(),
            capturedAtUtc.ToUniversalTime(), project.Status);
    }

    public TimeZoneInfo ValidateForRun(
        Guid tenantId, Guid projectId, DateTimeOffset cutoffUtc, DateTimeOffset acceptedAtUtc)
    {
        var cutoff = Microseconds(cutoffUtc);
        var accepted = Microseconds(acceptedAtUtc);
        if (SchemaVersion != ProjectTechnicalOfficeReportRuntimeContract.PinnedProjectProfileSchemaVersion ||
            Id == Guid.Empty || TenantId == Guid.Empty || Id != projectId || TenantId != tenantId ||
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(TimeZone) || Revision <= 0 || ConfigurationVersion <= 0 ||
            Status != ProjectStatus.Active || cutoff == default || accepted == default ||
            cutoff > Microseconds(CapturedAtUtc) || Microseconds(CapturedAtUtc) > accepted ||
            Microseconds(ConfigurationChangedAtUtc) > cutoff)
            throw Invalid("project_scope.invalid", "Pinned Project identity/configuration does not match the Run.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (TimeZoneNotFoundException)
        {
            throw Invalid("time_zone.invalid", "Project time zone is unknown.");
        }
        catch (InvalidTimeZoneException)
        {
            throw Invalid("time_zone.invalid", "Project time zone is invalid.");
        }
    }

    private static DateTimeOffset Microseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private static DomainRuleException Invalid(string code, string message) =>
        new($"reporting.project_technical_office.{code}", message);
}
