using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public static class ProjectQualityHseReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F08-SEMANTIC-001";
    public const string DefinitionCode = "project-quality-hse-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion = "pmcs.reporting.project-quality-hse.parameters/v1";
    public const string SnapshotSchemaVersion = "pmcs.reporting.project-quality-hse.snapshot/v1";
    public const string TemplateVersion = "1.0.0";
    public const string TemplateContentDigest =
        "5bd7a0ea06984076cfbfed7715329a59b1afeffbcade4378afefc1413c287a61";
    public const string RendererContractVersion = "pmcs.reporting.project-quality-hse.renderer/v1";
    public const string LayoutContractVersion = "pmcs.reporting.project-quality-hse.layout/v1";
    public const string PinnedProjectProfileSchemaVersion = "pmcs.reporting.project-quality-hse.project-profile/v1";
    public static readonly IReadOnlyCollection<string> RequiredSourcePermissions =
        ["quality.read", "hse.read", "hse.confidential.read"];
}

public sealed record ProjectQualityHseReportParameters;

public sealed record ProjectQualityHsePinnedProjectProfile(
    string SchemaVersion, Guid Id, Guid TenantId, string Code, string Name,
    string TimeZone, long Revision, long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAtUtc, DateTimeOffset CapturedAtUtc,
    ProjectStatus Status, ProjectFeatureState Quality, ProjectFeatureState Hse)
{
    public static ProjectQualityHsePinnedProjectProfile Capture(
        ProjectControlProfile project, DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.ConfigurationChangedAt.HasValue)
            throw Invalid("configuration.unversioned", "Project configuration must be versioned.");
        return new(ProjectQualityHseReportRuntimeContract.PinnedProjectProfileSchemaVersion,
            project.Id, project.TenantId, project.Code, project.Name, project.TimeZone,
            project.Revision, project.ConfigurationVersion,
            project.ConfigurationChangedAt.Value.ToUniversalTime(),
            capturedAtUtc.ToUniversalTime(), project.Status, project.Quality, project.Hse);
    }

    public TimeZoneInfo ValidateForRun(
        Guid tenantId, Guid projectId, DateTimeOffset cutoffUtc, DateTimeOffset acceptedAtUtc)
    {
        static DateTimeOffset Microseconds(DateTimeOffset value)
        {
            var utc = value.ToUniversalTime();
            return utc.AddTicks(-(utc.Ticks % 10));
        }
        if (SchemaVersion != ProjectQualityHseReportRuntimeContract.PinnedProjectProfileSchemaVersion ||
            Id == Guid.Empty || TenantId == Guid.Empty || Id != projectId || TenantId != tenantId ||
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(TimeZone) || Revision <= 0 || ConfigurationVersion <= 0 ||
            Status != ProjectStatus.Active || !Enum.IsDefined(Quality) || !Enum.IsDefined(Hse) ||
            cutoffUtc == default || acceptedAtUtc == default || CapturedAtUtc == default ||
            Microseconds(cutoffUtc) > Microseconds(CapturedAtUtc) ||
            Microseconds(CapturedAtUtc) > Microseconds(acceptedAtUtc) ||
            Microseconds(ConfigurationChangedAtUtc) > Microseconds(cutoffUtc))
            throw Invalid("project_scope.invalid", "Pinned Project identity/configuration does not match the Run.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (TimeZoneNotFoundException) { throw Invalid("time_zone.invalid", "Project time zone is unknown."); }
        catch (InvalidTimeZoneException) { throw Invalid("time_zone.invalid", "Project time zone is invalid."); }
    }

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"reporting.project_quality_hse.{suffix}", message);
}
