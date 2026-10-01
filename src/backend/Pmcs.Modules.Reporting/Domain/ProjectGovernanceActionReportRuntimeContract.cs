using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public static class ProjectGovernanceActionReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F09-SEMANTIC-001";
    public const string DefinitionCode = "project-governance-action-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion = "pmcs.reporting.project-governance-action.parameters/v1";
    public const string SnapshotSchemaVersion = "pmcs.reporting.project-governance-action.snapshot/v1";
    public const string TemplateVersion = "1.0.0";
    public const string TemplateContentDigest =
        "7fcb7af589562b09850491fe88d7067b0e20297d8fdb03148b0408db97b52a01";
    public const string RendererContractVersion = "pmcs.reporting.project-governance-action.renderer/v1";
    public const string LayoutContractVersion = "pmcs.reporting.project-governance-action.layout/v1";
    public const string PinnedProjectProfileSchemaVersion = "pmcs.reporting.project-governance-action.project-profile/v1";
    public static readonly IReadOnlyCollection<string> RequiredSourcePermissions =
        ["governance.read", "governance.sensitive.read", "actions.read"];
}

public sealed record ProjectGovernanceActionReportParameters;

public sealed record ProjectGovernanceActionPinnedProjectProfile(
    string SchemaVersion, Guid Id, Guid TenantId, string Code, string Name,
    string TimeZone, long Revision, long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAtUtc, DateTimeOffset CapturedAtUtc, ProjectStatus Status)
{
    public static ProjectGovernanceActionPinnedProjectProfile Capture(
        ProjectControlProfile project, DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.ConfigurationChangedAt.HasValue)
            throw Invalid("configuration.unversioned", "Project configuration must be versioned.");
        return new(ProjectGovernanceActionReportRuntimeContract.PinnedProjectProfileSchemaVersion,
            project.Id, project.TenantId, project.Code, project.Name, project.TimeZone,
            project.Revision, project.ConfigurationVersion,
            project.ConfigurationChangedAt.Value.ToUniversalTime(),
            capturedAtUtc.ToUniversalTime(), project.Status);
    }

    public TimeZoneInfo ValidateForRun(Guid tenantId, Guid projectId,
        DateTimeOffset cutoffUtc, DateTimeOffset acceptedAtUtc)
    {
        static DateTimeOffset Microseconds(DateTimeOffset value)
        {
            var utc = value.ToUniversalTime();
            return utc.AddTicks(-(utc.Ticks % 10));
        }
        if (SchemaVersion != ProjectGovernanceActionReportRuntimeContract.PinnedProjectProfileSchemaVersion ||
            Id == Guid.Empty || TenantId == Guid.Empty || Id != projectId || TenantId != tenantId ||
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(TimeZone) || Revision <= 0 || ConfigurationVersion <= 0 ||
            Status != ProjectStatus.Active || cutoffUtc == default ||
            acceptedAtUtc == default || CapturedAtUtc == default ||
            Microseconds(cutoffUtc) > Microseconds(CapturedAtUtc) ||
            Microseconds(CapturedAtUtc) > Microseconds(acceptedAtUtc) ||
            Microseconds(ConfigurationChangedAtUtc) > Microseconds(cutoffUtc))
            throw Invalid("project_scope.invalid", "Pinned project identity/configuration does not match the Run.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (TimeZoneNotFoundException) { throw Invalid("time_zone.invalid", "Unknown project time zone."); }
        catch (InvalidTimeZoneException) { throw Invalid("time_zone.invalid", "Invalid project time zone."); }
    }

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"reporting.project_governance_action.{suffix}", message);
}
