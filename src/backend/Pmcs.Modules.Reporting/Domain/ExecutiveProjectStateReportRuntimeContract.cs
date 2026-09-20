using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public static class ExecutiveProjectStateReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F03-SEMANTIC-001";
    public const string DefinitionCode = "executive-project-state-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion =
        "pmcs.reporting.executive-project-state.parameters/v1";
    public const string SnapshotSchemaVersion =
        "pmcs.reporting.executive-project-state.snapshot/v1";
    public const string PinnedProjectProfileSchemaVersion =
        "pmcs.reporting.executive-project-state.project-profile/v1";
}

public sealed record ExecutiveProjectStateReportParameters;

public sealed record ExecutiveProjectStatePinnedProjectProfile(
    string SchemaVersion,
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    string BaseCurrencyCode,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt,
    ProjectStatus Status)
{
    public static ExecutiveProjectStatePinnedProjectProfile Capture(ProjectControlProfile project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.ConfigurationChangedAt.HasValue)
        {
            throw new DomainRuleException(
                "reporting.executive_state.project_configuration.unversioned",
                "The Project profile must be versioned before it can be pinned.");
        }

        return new ExecutiveProjectStatePinnedProjectProfile(
            ExecutiveProjectStateReportRuntimeContract.PinnedProjectProfileSchemaVersion,
            project.Id,
            project.TenantId,
            project.Code,
            project.Name,
            project.TimeZone,
            project.BaseCurrencyCode,
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
                ExecutiveProjectStateReportRuntimeContract.PinnedProjectProfileSchemaVersion,
                StringComparison.Ordinal) ||
            Id == Guid.Empty || TenantId == Guid.Empty || Id != projectId || TenantId != tenantId ||
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(TimeZone) || string.IsNullOrWhiteSpace(BaseCurrencyCode) ||
            Revision <= 0 || ConfigurationVersion <= 0 || ConfigurationChangedAt == default ||
            ConfigurationChangedAt.ToUniversalTime() > acceptedAt || cutoff == default || cutoff > acceptedAt ||
            Status != ProjectStatus.Active)
        {
            throw new DomainRuleException(
                "reporting.executive_state.project_scope.invalid",
                "The pinned Project profile does not match the accepted report run.");
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
        "reporting.executive_state.time_zone.invalid",
        "The pinned Project time zone is unknown or invalid.");
}

public enum ExecutiveProjectStateReportReasonCode
{
    ProjectStateReportingNotConfigured = 1,
    OfficialSnapshotMissing = 2,
    OfficialSnapshotNoData = 3,
    OfficialSnapshotInsufficient = 4,
    CoverageInsufficient = 5,
    FreshnessStale = 6,
    ConfidenceLow = 7,
    ProjectConfigurationRevisionOutdated = 8,
    ApprovedSourceChangedAfterSnapshot = 9
}
