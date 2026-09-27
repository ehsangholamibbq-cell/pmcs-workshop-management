using System.Text.Json;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Modules.Reporting.Rendering;

internal sealed record PortfolioSummaryReportRenderRequest(
    Guid RunId, Guid OutputId, Guid SnapshotId, Guid TemplateVersionId,
    string DefinitionCode, string DefinitionVersion, string TemplateVersion,
    string TemplateContentDigest, string RendererContractVersion,
    string LayoutContractVersion, ReportFormat Format, string FileName,
    string VerificationCode, string ManifestSha256, string SnapshotSha256,
    string SourceManifestSha256, string SourceManifestJson,
    DateTimeOffset SourceCutoffUtc, ReportClassification Classification,
    PortfolioSummarySemanticSnapshot Snapshot);

internal interface IPortfolioSummaryReportRenderer
{
    ReportFormat Format { get; }
    RenderedReportArtifact Render(PortfolioSummaryReportRenderRequest request);
}

internal sealed class PortfolioSummaryReportRenderModel
{
    private PortfolioSummaryReportRenderModel(PortfolioSummaryReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
    }

    public PortfolioSummaryReportRenderRequest Request { get; }
    public PortfolioSummarySemanticSnapshot Snapshot { get; }

    public static PortfolioSummaryReportRenderModel Create(
        PortfolioSummaryReportRenderRequest request)
    {
        PortfolioSummaryReportRenderingContract.ValidateRequest(request);
        return new(request);
    }
}

internal static class PortfolioSummaryReportRenderingContract
{
    public static PortfolioSummarySemanticSnapshot Parse(
        string payloadJson, string sourceManifestJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<PortfolioSummarySemanticSnapshot>(
                payloadJson, CanonicalJson.SerializerOptions) ?? throw Invalid();
            ValidateSnapshot(snapshot, sourceManifestJson);
            return snapshot;
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw Invalid(exception);
        }
    }

    public static void ValidateRequest(PortfolioSummaryReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot, request.SourceManifestJson);
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            request.DefinitionCode != PortfolioSummaryReportRuntimeContract.DefinitionCode ||
            request.DefinitionVersion != PortfolioSummaryReportRuntimeContract.DefinitionVersion ||
            request.TemplateVersion != PortfolioSummaryReportRuntimeContract.TemplateVersion ||
            request.TemplateContentDigest != PortfolioSummaryReportRuntimeContract.TemplateContentDigest ||
            request.RendererContractVersion != PortfolioSummaryReportRuntimeContract.RendererContractVersion ||
            request.LayoutContractVersion != PortfolioSummaryReportRuntimeContract.LayoutContractVersion ||
            request.Format is not (ReportFormat.Pdf or ReportFormat.Xlsx) ||
            request.FileName != ReportArtifactIdentity.FileName(request.Snapshot, request.Format) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !Digest(request.ManifestSha256) ||
            request.SnapshotSha256 != CanonicalJson.Sha256(CanonicalJson.Serialize(request.Snapshot)) ||
            request.SourceManifestSha256 != request.Snapshot.SourceManifestSha256 ||
            request.SourceCutoffUtc != request.Snapshot.AsOfUtc ||
            request.Classification < ReportClassification.Confidential)
        {
            throw new ReportRenderingException("reporting.portfolio.render_request.invalid",
                transient: false, "F10 render identity differs from its immutable snapshot or template.");
        }
        var rebuilt = Rebuild(request.Snapshot);
        if (request.Classification != rebuilt.Classification)
            throw new ReportRenderingException("reporting.portfolio.render_request.invalid",
                transient: false, "F10 output classification differs from its immutable snapshot.");
    }

    private static void ValidateSnapshot(
        PortfolioSummarySemanticSnapshot snapshot, string sourceManifestJson)
    {
        try
        {
            if (snapshot.SchemaVersion != PortfolioSummaryReportRuntimeContract.SnapshotSchemaVersion ||
                snapshot.DefinitionCode != PortfolioSummaryReportRuntimeContract.DefinitionCode ||
                snapshot.AsOfUtc == default || snapshot.AsOfUtc.Offset != TimeSpan.Zero ||
                snapshot.TenantId == Guid.Empty || snapshot.Projects is null ||
                snapshot.CurrencyGroups is null || !Digest(snapshot.SourceManifestSha256) ||
                snapshot.Projects.Count != snapshot.AuthorizedProjectCount ||
                snapshot.Projects.Count > PortfolioSummaryReportRuntimeContract.MaximumProjects)
                throw Invalid();

            var manifest = JsonSerializer.Deserialize<PortfolioSummarySourceManifest>(
                sourceManifestJson, CanonicalJson.SerializerOptions) ?? throw Invalid();
            var rebuilt = Rebuild(snapshot);
            if (manifest.Version != PortfolioSummaryReportRuntimeContract.SourceManifestVersion ||
                manifest.TenantId != snapshot.TenantId ||
                manifest.SourceCutoffUtc != snapshot.AsOfUtc ||
                CanonicalJson.Serialize(snapshot) != rebuilt.PayloadJson ||
                CanonicalJson.Serialize(manifest) != rebuilt.SourceManifestJson ||
                CanonicalJson.Sha256(CanonicalJson.Serialize(manifest)) != snapshot.SourceManifestSha256)
                throw Invalid();
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or
            Pmcs.BuildingBlocks.Domain.DomainRuleException or OverflowException or
            ArgumentException or NullReferenceException)
        {
            throw Invalid(exception);
        }
    }

    private static ReportSnapshot Rebuild(PortfolioSummarySemanticSnapshot snapshot) =>
        PortfolioSummaryReportSnapshotBuilder.Build(Guid.NewGuid(),
            new PortfolioSummarySelection(snapshot.TenantId, Guid.NewGuid(),
                snapshot.AsOfUtc, snapshot.Projects), snapshot.AsOfUtc);

    private static bool Digest(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static ReportRenderingException Invalid(Exception? inner = null) => new(
        "reporting.portfolio.snapshot.payload_invalid", transient: false,
        "F10 snapshot or source manifest violates its certified contract.", inner);
}
