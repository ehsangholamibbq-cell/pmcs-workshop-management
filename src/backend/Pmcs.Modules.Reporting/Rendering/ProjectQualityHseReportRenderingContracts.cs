using System.Text.Json;
using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectQualityHseReportRenderSnapshot
{
    public static ProjectQualityHseReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectQualityHseReportSemanticSnapshot>(
                payloadJson, CanonicalJson.SerializerOptions)
                ?? throw ProjectQualityHseReportRenderingContract.InvalidSnapshot("F08 snapshot is empty.");
            ProjectQualityHseReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw ProjectQualityHseReportRenderingContract.InvalidSnapshot(
                "F08 snapshot cannot be rendered.", exception);
        }
    }
}

internal sealed record ProjectQualityHseReportRenderRequest(
    Guid RunId, Guid OutputId, Guid SnapshotId, Guid TemplateVersionId,
    string DefinitionCode, string DefinitionVersion, string TemplateVersion,
    string TemplateContentDigest, string RendererContractVersion,
    string LayoutContractVersion, ReportFormat Format, string FileName,
    string VerificationCode, string ManifestSha256, string SnapshotSha256,
    string SourceManifestSha256, DateTimeOffset SourceCutoffUtc,
    ProjectQualityHseReportSemanticSnapshot Snapshot);

internal interface IProjectQualityHseReportRenderer
{
    ReportFormat Format { get; }
    RenderedReportArtifact Render(ProjectQualityHseReportRenderRequest request);
}

internal sealed class ProjectQualityHseReportRenderModel
{
    private ProjectQualityHseReportRenderModel(ProjectQualityHseReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        Quality = Snapshot.Quality.Rows.ToArray();
        Hse = Snapshot.Hse.Rows.ToArray();
    }

    public ProjectQualityHseReportRenderRequest Request { get; }
    public ProjectQualityHseReportSemanticSnapshot Snapshot { get; }
    public QualityHseReportingFact[] Quality { get; }
    public QualityHseReportingFact[] Hse { get; }
    public int FactCount => Quality.Length + Hse.Length;

    public static ProjectQualityHseReportRenderModel Create(ProjectQualityHseReportRenderRequest request)
    {
        ProjectQualityHseReportRenderingContract.ValidateRequest(request);
        return new(request);
    }
}

internal static class ProjectQualityHseReportRenderingContract
{
    public static void ValidateRequest(ProjectQualityHseReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            request.DefinitionCode != ProjectQualityHseReportRuntimeContract.DefinitionCode ||
            request.DefinitionVersion != ProjectQualityHseReportRuntimeContract.DefinitionVersion ||
            request.TemplateVersion != ProjectQualityHseReportRuntimeContract.TemplateVersion ||
            request.TemplateContentDigest != ProjectQualityHseReportRuntimeContract.TemplateContentDigest ||
            request.RendererContractVersion != ProjectQualityHseReportRuntimeContract.RendererContractVersion ||
            request.LayoutContractVersion != ProjectQualityHseReportRuntimeContract.LayoutContractVersion ||
            request.Format is not (ReportFormat.Pdf or ReportFormat.Xlsx) ||
            request.FileName != ReportArtifactIdentity.FileName(request.Snapshot, request.Format) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !Digest(request.ManifestSha256) || !Digest(request.SnapshotSha256) ||
            request.SourceManifestSha256 != request.Snapshot.SourceManifestSha256 ||
            request.SourceCutoffUtc.ToUniversalTime() != request.Snapshot.Cutoff.SourceCutoffUtc ||
            CanonicalJson.Sha256(CanonicalJson.Serialize(request.Snapshot)) != request.SnapshotSha256)
            throw new ReportRenderingException("reporting.project_quality_hse.render_request.invalid",
                transient: false, "F08 render identity differs from its immutable snapshot or template.");
    }

    public static void ValidateSnapshot(ProjectQualityHseReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != ProjectQualityHseReportRuntimeContract.SnapshotSchemaVersion ||
            snapshot.SemanticContractId != ProjectQualityHseReportRuntimeContract.SemanticContractId ||
            snapshot.DefinitionCode != ProjectQualityHseReportRuntimeContract.DefinitionCode ||
            snapshot.DefinitionVersion != ProjectQualityHseReportRuntimeContract.DefinitionVersion ||
            snapshot.PolicyVersion != ProjectQualityHseReportingContract.PolicyVersion ||
            snapshot.DataStatus is not (ReportDataStatus.NotConfigured or ReportDataStatus.NoData or
                ReportDataStatus.InsufficientData or ReportDataStatus.Available) ||
            snapshot.Classification is not (ReportClassification.Confidential or ReportClassification.Restricted) ||
            snapshot.Reasons is null || snapshot.Parameters is null || snapshot.Project is null ||
            snapshot.Cutoff is null || snapshot.Quality is null || snapshot.Hse is null ||
            !Digest(snapshot.SourceManifestSha256) || !Digest(snapshot.SemanticSha256))
            throw InvalidSnapshot("F08 snapshot identity, classification or sections are invalid.");

        var project = snapshot.Project;
        var cutoff = snapshot.Cutoff;
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty ||
            string.IsNullOrWhiteSpace(project.Code) || project.Code.Length > 160 ||
            string.IsNullOrWhiteSpace(project.Name) || project.Name.Length > 400 ||
            project.Revision <= 0 || project.ConfigurationVersion <= 0 ||
            project.ConfigurationChangedAtUtc == default || cutoff.SourceCutoffUtc == default ||
            project.ConfigurationChangedAtUtc.Offset != TimeSpan.Zero ||
            cutoff.SourceCutoffUtc.Offset != TimeSpan.Zero ||
            project.ConfigurationChangedAtUtc > cutoff.SourceCutoffUtc ||
            cutoff.CutoffLocalDate == default || !Enum.IsDefined(project.Quality) ||
            !Enum.IsDefined(project.Hse))
            throw InvalidSnapshot("F08 pinned project or cutoff is invalid.");
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone);
            if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff.SourceCutoffUtc, zone).DateTime) !=
                cutoff.CutoffLocalDate)
                throw InvalidSnapshot("F08 project local cutoff contradicts its time zone.");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or
            InvalidTimeZoneException or ArgumentNullException)
        {
            throw InvalidSnapshot("F08 project time zone is unknown.", exception);
        }

        ValidateSection(snapshot.Quality, cutoff.SourceCutoffUtc, quality: true);
        ValidateSection(snapshot.Hse, cutoff.SourceCutoffUtc, quality: false);
        var statuses = new[] { snapshot.Quality.Status, snapshot.Hse.Status };
        var expected = statuses.Contains(QualityHseReportingStatus.InsufficientData)
            ? ReportDataStatus.InsufficientData
            : statuses.Contains(QualityHseReportingStatus.Available)
                ? ReportDataStatus.Available
                : statuses.All(x => x == QualityHseReportingStatus.NotConfigured)
                    ? ReportDataStatus.NotConfigured : ReportDataStatus.NoData;
        if (snapshot.DataStatus != expected ||
            snapshot.Classification != (snapshot.Quality.Classification == ReportClassification.Restricted ||
                snapshot.Hse.Classification == ReportClassification.Restricted
                ? ReportClassification.Restricted : ReportClassification.Confidential) ||
            !snapshot.Reasons.SequenceEqual(snapshot.Quality.Reasons.Concat(snapshot.Hse.Reasons)
                .Distinct().OrderBy(x => x)))
            throw InvalidSnapshot("F08 status, classification or reasons contradict its sections.");
    }

    private static void ValidateSection(ProjectQualityHseReportSection section,
        DateTimeOffset cutoff, bool quality)
    {
        if (!Enum.IsDefined(section.Status) ||
            section.Classification is not (ReportClassification.Confidential or ReportClassification.Restricted) ||
            section.Rows is null || section.Reasons is null ||
            section.Reasons.Any(x => !Enum.IsDefined(x)) ||
            section.Reasons.Distinct().Count() != section.Reasons.Count ||
            section.Rows.Count > ProjectQualityHseReportingContract.MaximumFacts)
            throw InvalidSnapshot("F08 section status, reasons or row budget is invalid.");
        if (section.Status is QualityHseReportingStatus.NotConfigured or QualityHseReportingStatus.InsufficientData)
        {
            if (section.OfficialCount is not null || section.Rows.Count != 0 || section.Reasons.Count == 0)
                throw InvalidSnapshot("F08 unavailable section cannot publish a partial count.");
        }
        else if (section.OfficialCount != section.Rows.Count ||
            section.Status == QualityHseReportingStatus.NoData && section.Rows.Count != 0 ||
            section.Status == QualityHseReportingStatus.Available && section.Rows.Count == 0)
            throw InvalidSnapshot("F08 official section count is invalid.");

        var ids = new HashSet<Guid>();
        QualityHseReportingFact? prior = null;
        foreach (var row in section.Rows)
        {
            if (row is null || row.Id == Guid.Empty || !ids.Add(row.Id) ||
                string.IsNullOrWhiteSpace(row.Number) || row.Number.Length > 80 ||
                row.OfficialAtUtc == default || row.OfficialAtUtc.Offset != TimeSpan.Zero ||
                row.OfficialAtUtc > cutoff || !ValidFact(row, quality) ||
                prior is not null && (prior.OfficialAtUtc > row.OfficialAtUtc ||
                    prior.OfficialAtUtc == row.OfficialAtUtc && prior.Id.CompareTo(row.Id) >= 0))
                throw InvalidSnapshot("F08 section contains an invalid or noncanonical fact.");
            prior = row;
        }
    }

    private static bool ValidFact(QualityHseReportingFact row, bool quality) => row.Kind switch
    {
        QualityHseFactKind.InspectionRequested when quality => row.State == "Requested" && row.Hours is null,
        QualityHseFactKind.InspectionResult when quality =>
            (row.State is "Pass" or "PassWithObservation" or "Fail" or "HoldOrDeferred" or
                "NotReadyOrNotInspected") && row.Hours is null,
        QualityHseFactKind.QualityTest when quality =>
            (row.State is "Pass" or "Fail" or "Inconclusive") && row.Hours is null,
        QualityHseFactKind.ToolboxTalk when !quality => row.State == "Recorded" && row.Hours is null,
        QualityHseFactKind.ExposureHours when !quality => row.State == "Approved" && row.Hours > 0,
        _ => false
    };

    private static bool Digest(string? value) => value is { Length: 64 } &&
        value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static ReportRenderingException InvalidSnapshot(string message, Exception? inner = null) =>
        new("reporting.project_quality_hse.snapshot.payload_invalid", transient: false, message, inner);
}
