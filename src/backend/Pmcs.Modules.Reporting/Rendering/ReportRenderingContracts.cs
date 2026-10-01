using System.Text.Json;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Endpoints;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed record DailyReportRenderSnapshot(
    string SchemaVersion,
    string DefinitionCode,
    string TemplateVersion,
    ReportDataStatus DataStatus,
    ReportProjectRenderIdentity Project,
    DateTimeOffset AsOfUtc,
    DailyReportReportParameters Parameters,
    Guid? RootReportId,
    Guid? CurrentOfficialReportId,
    string SourceManifestSha256,
    IReadOnlyCollection<DailyReportReportingVersion> Versions)
{
    public static DailyReportRenderSnapshot Parse(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<DailyReportRenderSnapshot>(
                payloadJson,
                CanonicalJson.SerializerOptions)
                ?? throw new ReportRenderingException(
                    "reporting.snapshot.payload_invalid",
                    transient: false,
                    "Report snapshot payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new ReportRenderingException(
                "reporting.snapshot.payload_invalid",
                transient: false,
                "Report snapshot payload cannot be rendered.",
                exception);
        }
    }
}

internal sealed record ReportProjectRenderIdentity(
    Guid Id,
    string Code,
    string Name,
    string TimeZone,
    long Revision);

internal sealed record ReportRenderRequest(
    Guid RunId,
    Guid OutputId,
    Guid SnapshotId,
    Guid TemplateVersionId,
    string DefinitionCode,
    string TemplateVersion,
    string TemplateContentDigest,
    string RendererContractVersion,
    string LayoutContractVersion,
    ReportFormat Format,
    string FileName,
    string VerificationCode,
    string ManifestSha256,
    string SnapshotSha256,
    string SourceManifestSha256,
    DateTimeOffset SourceCutoffUtc,
    DailyReportRenderSnapshot Snapshot);

internal sealed record RenderedReportArtifact(
    ReportFormat Format,
    string FileName,
    string ContentType,
    byte[] Bytes,
    string Sha256,
    string ManifestSha256,
    string VerificationCode);

internal interface IReportRenderer
{
    ReportFormat Format { get; }

    RenderedReportArtifact Render(ReportRenderRequest request);
}

internal sealed class ReportRenderingException(
    string code,
    bool transient,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;

    public bool Transient { get; } = transient;
}
