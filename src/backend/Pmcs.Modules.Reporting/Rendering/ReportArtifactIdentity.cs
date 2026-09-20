using System.Security.Cryptography;
using System.Text;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ReportArtifactIdentity
{
    private static readonly Guid OutputNamespace =
        Guid.Parse("e49803fa-9876-4ed7-ae13-05a7b7ace001");
    private static readonly Guid DocumentNamespace =
        Guid.Parse("e49803fa-9876-4ed7-ae13-05a7b7ace002");

    public static Guid OutputId(Guid runId, ReportFormat format) =>
        DeterministicGuid(OutputNamespace, $"{runId:N}:{format}");

    public static Guid DocumentId(Guid outputId) =>
        DeterministicGuid(DocumentNamespace, outputId.ToString("N"));

    public static ReportArtifactManifest CreateManifest(
        ReportRun run,
        ReportSnapshot snapshot,
        ReportTemplateVersionRecord template,
        ReportFormat format)
    {
        var contentType = ContentType(format);
        var canonicalJson = CanonicalJson.Serialize(new
        {
            schemaVersion = "pmcs.reporting.output-manifest/v1",
            definitionCode = run.DefinitionCode,
            templateVersion = run.TemplateVersion,
            templateContentDigest = template.ContentDigest,
            rendererContractVersion = template.RendererContractVersion,
            layoutContractVersion = template.LayoutContractVersion,
            snapshotSchemaVersion = snapshot.SchemaVersion,
            snapshotSha256 = snapshot.Sha256,
            sourceManifestSha256 = snapshot.SourceManifestSha256,
            sourceCutoffUtc = snapshot.SourceCutoffUtc,
            dataStatus = snapshot.DataStatus,
            format,
            contentType
        });
        var manifestSha256 = CanonicalJson.Sha256(canonicalJson);
        return new ReportArtifactManifest(
            canonicalJson,
            manifestSha256,
            VerificationCode(manifestSha256),
            contentType);
    }

    public static string FileName(DailyReportRenderSnapshot snapshot, ReportFormat format)
    {
        var current = snapshot.CurrentOfficialReportId.HasValue
            ? snapshot.Versions.SingleOrDefault(
                version => version.ReportId == snapshot.CurrentOfficialReportId.Value)
            : null;
        var date = current?.ReportDate ?? DateOnly.FromDateTime(snapshot.AsOfUtc.UtcDateTime);
        var revision = current?.VersionNumber ?? 0;
        var projectCode = SafeFileSegment(snapshot.Project.Code);
        var extension = format switch
        {
            ReportFormat.Pdf => "pdf",
            ReportFormat.Xlsx => "xlsx",
            _ => throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                "Output format is not supported by the certified template.")
        };
        return $"daily-report-{projectCode}-{PersianReportFormatting.FormatDate(date, persianDigits: false).Replace('/', '-')}-r{revision}.{extension}";
    }

    public static string FileName(ProjectPeriodicReportSemanticSnapshot snapshot, ReportFormat format)
    {
        var projectCode = SafeFileSegment(snapshot.Project.Code);
        var period = snapshot.Period.Kind == ProjectReportPeriodKind.Weekly ? "weekly" : "monthly";
        var start = PersianReportFormatting.FormatDate(
            snapshot.Period.StartLocalDate,
            persianDigits: false).Replace('/', '-');
        var end = PersianReportFormatting.FormatDate(
            snapshot.Period.EndLocalDateExclusive.AddDays(-1),
            persianDigits: false).Replace('/', '-');
        var extension = format switch
        {
            ReportFormat.Pdf => "pdf",
            ReportFormat.Xlsx => "xlsx",
            _ => throw new ReportRenderingException(
                "reporting.format.unsupported",
                transient: false,
                "Output format is not supported by the certified template.")
        };
        return $"project-{period}-{projectCode}-{start}-{end}.{extension}";
    }

    public static string ContentType(ReportFormat format) => format switch
    {
        ReportFormat.Pdf => "application/pdf",
        ReportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => throw new ReportRenderingException(
            "reporting.format.unsupported",
            transient: false,
            "Output format is not supported by the certified template.")
    };

    public static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string VerificationCode(string manifestSha256)
    {
        var prefix = manifestSha256[..20].ToUpperInvariant();
        return $"RPT-{prefix[..4]}-{prefix[4..8]}-{prefix[8..12]}-{prefix[12..16]}-{prefix[16..20]}";
    }

    private static Guid DeterministicGuid(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);
        var hash = SHA256.HashData(input);
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }

    private static string SafeFileSegment(string value)
    {
        var normalized = new string(value.Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '-')
            .Take(48)
            .ToArray()).Trim('-');
        return normalized.Length == 0 ? "project" : normalized;
    }
}

internal sealed record ReportArtifactManifest(
    string CanonicalJson,
    string Sha256,
    string VerificationCode,
    string ContentType);
