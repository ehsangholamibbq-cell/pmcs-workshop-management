using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.Reporting.Domain;

public sealed class ReportSnapshot
{
    private ReportSnapshot()
    {
    }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string SchemaVersion { get; private set; } = string.Empty;
    public ReportDataStatus DataStatus { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public string SourceManifestJson { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public string SourceManifestSha256 { get; private set; } = string.Empty;
    public ReportClassification Classification { get; private set; }
    public DateTimeOffset BuiltAt { get; private set; }
    public DateTimeOffset SourceCutoffUtc { get; private set; }

    public static ReportSnapshot Create(
        Guid id,
        Guid runId,
        Guid tenantId,
        Guid projectId,
        string schemaVersion,
        ReportDataStatus dataStatus,
        string payloadJson,
        string sourceManifestJson,
        ReportClassification classification,
        DateTimeOffset builtAt,
        DateTimeOffset sourceCutoffUtc)
    {
        if (id == Guid.Empty || runId == Guid.Empty || tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            throw new DomainRuleException("reporting.snapshot.identity.required", "Snapshot identities are required.");
        }

        if (!Enum.IsDefined(dataStatus) || dataStatus == ReportDataStatus.Pending ||
            !Enum.IsDefined(classification))
        {
            throw new DomainRuleException("reporting.snapshot.state.invalid", "Snapshot state is invalid.");
        }

        var normalizedPayload = CanonicalJson.Normalize(Parse(payloadJson, "reporting.snapshot.payload.invalid"));
        var normalizedManifest = CanonicalJson.Normalize(Parse(
            sourceManifestJson,
            "reporting.snapshot.source_manifest.invalid"));
        return new ReportSnapshot
        {
            Id = id,
            RunId = runId,
            TenantId = tenantId,
            ProjectId = projectId,
            SchemaVersion = Required(schemaVersion, 80, "reporting.snapshot.schema.invalid"),
            DataStatus = dataStatus,
            PayloadJson = normalizedPayload,
            SourceManifestJson = normalizedManifest,
            Sha256 = CanonicalJson.Sha256(normalizedPayload),
            SourceManifestSha256 = CanonicalJson.Sha256(normalizedManifest),
            Classification = classification,
            BuiltAt = builtAt.ToUniversalTime(),
            SourceCutoffUtc = sourceCutoffUtc.ToUniversalTime()
        };
    }

    private static System.Text.Json.JsonElement Parse(string json, string code)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new DomainRuleException(code, "Snapshot JSON is invalid.");
        }
    }

    private static string Required(string value, int maximumLength, string code)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, "A required snapshot value is missing or too long.");
        }

        return normalized;
    }
}
