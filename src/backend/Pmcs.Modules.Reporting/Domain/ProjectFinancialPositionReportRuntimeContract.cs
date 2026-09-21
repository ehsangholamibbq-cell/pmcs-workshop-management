using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public static class ProjectFinancialPositionReportRuntimeContract
{
    public const string SemanticContractId = "PMCS-RPT1-F05-SEMANTIC-001";
    public const string DefinitionCode = "project-financial-position-certified";
    public const string DefinitionVersion = "1.0.0";
    public const string ParameterSchemaVersion =
        "pmcs.reporting.project-financial-position.parameters/v1";
    public const string SnapshotSchemaVersion =
        "pmcs.reporting.project-financial-position.snapshot/v1";
    public const string TemplateVersion = "1.0.0";
    public const string TemplateContentDigest =
        "e6ad4cbf2559d825d70b1579e687e7f9ce15020afaf697692e263f18480f18e4";
    public const string RendererContractVersion =
        "pmcs.reporting.project-financial-position.renderer/v1";
    public const string LayoutContractVersion =
        "pmcs.reporting.project-financial-position.layout/v1";
    public const string PinnedProjectProfileSchemaVersion =
        "pmcs.reporting.project-financial-position.project-profile/v1";
}

public sealed record ProjectFinancialPositionReportParameters;

public sealed record ProjectFinancialPositionPinnedProjectProfile(
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
    DateTimeOffset CapturedAtUtc,
    ProjectStatus Status)
{
    public static ProjectFinancialPositionPinnedProjectProfile Capture(
        ProjectControlProfile project,
        DateTimeOffset capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.ConfigurationChangedAt.HasValue)
        {
            throw new DomainRuleException(
                "reporting.project_financial_position.project_configuration.unversioned",
                "The Project profile must be versioned before it can be pinned.");
        }

        return new ProjectFinancialPositionPinnedProjectProfile(
            ProjectFinancialPositionReportRuntimeContract.PinnedProjectProfileSchemaVersion,
            project.Id,
            project.TenantId,
            project.Code,
            project.Name,
            project.TimeZone,
            NormalizeCurrency(project.BaseCurrencyCode),
            project.Revision,
            project.ConfigurationVersion,
            project.ConfigurationChangedAt.Value.ToUniversalTime(),
            capturedAtUtc.ToUniversalTime(),
            project.Status);
    }

    public TimeZoneInfo ValidateForRun(
        Guid tenantId,
        Guid projectId,
        DateTimeOffset sourceCutoffUtc,
        DateTimeOffset acceptedAtUtc)
    {
        var cutoff = ToMicrosecondPrecision(sourceCutoffUtc);
        var acceptedAt = ToMicrosecondPrecision(acceptedAtUtc);
        var configurationChangedAt = ToMicrosecondPrecision(ConfigurationChangedAt);
        var capturedAt = ToMicrosecondPrecision(CapturedAtUtc);
        if (!string.Equals(
                SchemaVersion,
                ProjectFinancialPositionReportRuntimeContract.PinnedProjectProfileSchemaVersion,
                StringComparison.Ordinal) ||
            Id == Guid.Empty || TenantId == Guid.Empty || Id != projectId || TenantId != tenantId ||
            string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(TimeZone) || !IsCurrency(BaseCurrencyCode) ||
            Revision <= 0 || ConfigurationVersion <= 0 || ConfigurationChangedAt == default ||
            CapturedAtUtc == default || acceptedAt == default || cutoff == default ||
            configurationChangedAt > capturedAt || capturedAt > acceptedAt || cutoff > capturedAt ||
            Status != ProjectStatus.Active)
        {
            throw new DomainRuleException(
                "reporting.project_financial_position.project_scope.invalid",
                "The pinned Project profile does not match the accepted financial position report run.");
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

    private static string NormalizeCurrency(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!IsCurrency(normalized))
        {
            throw new DomainRuleException(
                "reporting.project_financial_position.base_currency.invalid",
                "The pinned Project Base Currency is invalid.");
        }

        return normalized;
    }

    private static bool IsCurrency(string value) =>
        value.Length == 3 && value.All(char.IsAsciiLetterUpper);

    private static DateTimeOffset ToMicrosecondPrecision(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % 10));
    }

    private static DomainRuleException InvalidTimeZone() => new(
        "reporting.project_financial_position.time_zone.invalid",
        "The pinned Project time zone is unknown or invalid.");
}
