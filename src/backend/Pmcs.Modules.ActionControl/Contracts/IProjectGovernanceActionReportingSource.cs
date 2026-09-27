using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pmcs.Modules.ActionControl.Contracts;

public interface IProjectGovernanceActionReportingSource
{
    Task<ProjectGovernanceActionReportingResult> LoadAsync(
        Guid tenantId, Guid projectId, DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc, CancellationToken cancellationToken = default);
}

public static class ProjectGovernanceActionReportingContract
{
    public const string Version = "pmcs.action-control.project-reporting/v1";
    public const string PolicyVersion = "pmcs.action-control.reporting-policy/v1";
    public const string ManifestVersion = "pmcs.action-control.reporting-manifest/v1";
    public const string ActionClassificationPolicy = "all-management-actions-restricted/v1";
    public const int MaximumPerRegister = 20_000;
    public const int MaximumFacts = 60_000;
    public const int MaximumSnapshotBytes = 8 * 1024 * 1024;
    public const int MaximumReadSeconds = 30;
}

public enum GovernanceActionReportingStatus { NotConfigured = 1, NoData = 2, InsufficientData = 3, Available = 4 }
public enum GovernanceActionReportingClassification { Confidential = 2, Restricted = 3 }
public enum GovernanceActionReportingReason
{
    GovernanceSourceNotConfigured = 1,
    NoOfficialIssue = 2, NoOfficialRisk = 3, NoSubmittedDecision = 4,
    NoRaisedEscalation = 5, NoOfficialAction = 6,
    HistoricalTransitionUnavailable = 7, SourceCoverageIncomplete = 8,
    MatrixVersionUnavailable = 9, SlaRuleUnavailable = 10,
    WorkingCalendarUnavailable = 11, NoAssessedRisk = 12
}
public enum GovernanceActionFactKind
{
    Issue = 1, Risk = 2, DecisionRequest = 3, DecisionRecord = 4,
    Escalation = 5, Action = 6
}

// No narrative, person, evidence URI, source snapshot or selected decision option crosses this boundary.
public sealed record GovernanceActionReportingFact(
    Guid Id, string Number, GovernanceActionFactKind Kind, string State,
    DateTimeOffset OfficialAtUtc, DateOnly? DueLocalDate,
    DateTimeOffset? SlaDueAtUtc, string? Rating,
    int? MatrixVersion, GovernanceActionReportingClassification Classification);

public sealed record GovernanceActionReportingSection(
    GovernanceActionReportingStatus Status, int? OfficialCount,
    IReadOnlyCollection<GovernanceActionReportingFact> Rows,
    IReadOnlyCollection<GovernanceActionReportingReason> Reasons,
    GovernanceActionReportingClassification Classification);

public sealed record GovernanceActionReportingRegister(
    string Name, int SourceCount, string FactSha256);

public sealed record ProjectGovernanceActionSourceManifest(
    string ManifestVersion, string ContractVersion, string PolicyVersion,
    string ActionClassificationPolicy,
    Guid TenantId, Guid ProjectId, DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc, DateTimeOffset WatermarkUtc,
    long ProjectConfigurationVersion, DateTimeOffset ProjectConfigurationChangedAtUtc,
    GovernanceActionReportingClassification Classification,
    IReadOnlyCollection<GovernanceActionReportingRegister> Registers);

public sealed record ProjectGovernanceActionReportingResult(
    string ContractVersion, string PolicyVersion, Guid TenantId, Guid ProjectId,
    DateOnly CutoffLocalDate, DateTimeOffset SourceCutoffUtc,
    GovernanceActionReportingClassification Classification,
    GovernanceActionReportingStatus DataStatus,
    IReadOnlyCollection<GovernanceActionReportingReason> Reasons,
    GovernanceActionReportingSection Issues, GovernanceActionReportingSection Risks,
    GovernanceActionReportingSection Decisions, GovernanceActionReportingSection Escalations,
    GovernanceActionReportingSection Actions,
    ProjectGovernanceActionSourceManifest SourceManifest,
    string SourceManifestSha256, string SemanticSha256);

public static class GovernanceActionReportingHash
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Compute<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, Options);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var child in element.EnumerateArray()) Write(writer, child);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String: writer.WriteStringValue(element.GetString()); break;
            case JsonValueKind.Number: writer.WriteRawValue(element.GetRawText(), skipInputValidation: true); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new ArgumentOutOfRangeException(nameof(element));
        }
    }

    public static string Result(ProjectGovernanceActionReportingResult result) => Compute(new
    {
        result.ContractVersion, result.PolicyVersion, result.TenantId, result.ProjectId,
        result.CutoffLocalDate, result.SourceCutoffUtc, result.Classification,
        result.DataStatus, result.Reasons, result.Issues, result.Risks,
        result.Decisions, result.Escalations, result.Actions, result.SourceManifestSha256
    });
}
