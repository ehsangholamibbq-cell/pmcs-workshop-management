using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pmcs.Modules.QualitySafety.Contracts;

public interface IProjectQualityHseReportingSource
{
    Task<ProjectQualityHseReportingResult> LoadAsync(
        Guid tenantId, Guid projectId, DateOnly cutoffLocalDate,
        DateTimeOffset sourceCutoffUtc, CancellationToken cancellationToken = default);
}

public static class ProjectQualityHseReportingContract
{
    public const string Version = "pmcs.quality-safety.project-reporting/v1";
    public const string PolicyVersion = "pmcs.quality-safety.reporting-policy/v1";
    public const string ManifestVersion = "pmcs.quality-safety.reporting-manifest/v1";
    public const int MaximumPerRegister = 20_000;
    public const int MaximumFacts = 50_000;
    public const int MaximumReadSeconds = 30;
}

public enum QualityHseReportingStatus { NotConfigured = 1, NoData = 2, InsufficientData = 3, Available = 4 }
public enum QualityHseReportingClassification { Confidential = 2, Restricted = 3 }
public enum QualityHseReportingReason
{
    QualityNotConfigured = 1, HseNotConfigured = 2,
    NoOfficialQualityFact = 3, NoOfficialHseFact = 4,
    HistoricalTransitionUnavailable = 5, SourceCoverageIncomplete = 6,
    ConfigurationHistoryUnavailable = 7, ExposureBasisIncomplete = 8,
    RestrictedPublicationUnavailable = 9
}
public enum QualityHseFactKind
{
    InspectionRequested = 1, InspectionResult = 2, QualityTest = 3,
    ToolboxTalk = 4, ExposureHours = 5
}

// Only minimized, non-personal application facts cross the module boundary.
public sealed record QualityHseReportingFact(
    Guid Id, string Number, QualityHseFactKind Kind, string State,
    DateTimeOffset OfficialAtUtc, decimal? Hours = null);

public sealed record QualityHseReportingSection(
    QualityHseReportingStatus Status, int? OfficialCount,
    IReadOnlyCollection<QualityHseReportingFact> Rows,
    IReadOnlyCollection<QualityHseReportingReason> Reasons,
    QualityHseReportingClassification Classification);

public sealed record QualityHseReportingRegister(
    string Name, int SourceCount, string FactSha256);

public sealed record ProjectQualityHseSourceManifest(
    string ManifestVersion, string ContractVersion, string PolicyVersion,
    Guid TenantId, Guid ProjectId, DateOnly CutoffLocalDate,
    DateTimeOffset SourceCutoffUtc, DateTimeOffset WatermarkUtc,
    long ProjectConfigurationVersion, DateTimeOffset ProjectConfigurationChangedAtUtc,
    long? QualitySafetyConfigurationRevision, DateTimeOffset? QualitySafetyConfigurationChangedAtUtc,
    string? QualityMode, string? HseMode, bool QualityReady, bool HseReady,
    Guid? QualityMatrixVersionId, Guid? HseMatrixVersionId,
    bool QualityEnabled, bool HseEnabled,
    QualityHseReportingClassification Classification,
    IReadOnlyCollection<QualityHseReportingRegister> Registers);

public sealed record ProjectQualityHseReportingResult(
    string ContractVersion, string PolicyVersion, Guid TenantId, Guid ProjectId,
    DateOnly CutoffLocalDate, DateTimeOffset SourceCutoffUtc,
    QualityHseReportingClassification Classification,
    QualityHseReportingStatus DataStatus,
    IReadOnlyCollection<QualityHseReportingReason> Reasons,
    QualityHseReportingSection Quality, QualityHseReportingSection Hse,
    ProjectQualityHseSourceManifest SourceManifest,
    string SourceManifestSha256, string SemanticSha256);

public static class QualityHseReportingHash
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

    public static string Result(ProjectQualityHseReportingResult result) =>
        Compute(new
        {
            result.ContractVersion, result.PolicyVersion, result.TenantId, result.ProjectId,
            result.CutoffLocalDate, result.SourceCutoffUtc, result.Classification,
            result.DataStatus, result.Reasons, result.Quality, result.Hse,
            result.SourceManifestSha256
        });
}
