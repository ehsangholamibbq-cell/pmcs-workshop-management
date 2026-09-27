using System.Text.Json;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.TechnicalOffice.Contracts;
using Pmcs.Modules.TechnicalOffice.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectTechnicalOfficeReportRenderSnapshot
{
    public static ProjectTechnicalOfficeReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectTechnicalOfficeReportSemanticSnapshot>(
                payloadJson, CanonicalJson.SerializerOptions)
                ?? throw ProjectTechnicalOfficeReportRenderingContract.InvalidSnapshot(
                    "F07 snapshot payload is empty.");
            ProjectTechnicalOfficeReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw ProjectTechnicalOfficeReportRenderingContract.InvalidSnapshot(
                "F07 snapshot payload cannot be rendered.", exception);
        }
    }
}

internal sealed record ProjectTechnicalOfficeReportRenderRequest(
    Guid RunId, Guid OutputId, Guid SnapshotId, Guid TemplateVersionId,
    string DefinitionCode, string DefinitionVersion, string TemplateVersion,
    string TemplateContentDigest, string RendererContractVersion,
    string LayoutContractVersion, ReportFormat Format, string FileName,
    string VerificationCode, string ManifestSha256, string SnapshotSha256,
    string SourceManifestSha256, DateTimeOffset SourceCutoffUtc,
    ProjectTechnicalOfficeReportSemanticSnapshot Snapshot);

internal interface IProjectTechnicalOfficeReportRenderer
{
    ReportFormat Format { get; }
    RenderedReportArtifact Render(ProjectTechnicalOfficeReportRenderRequest request);
}

internal sealed class ProjectTechnicalOfficeReportRenderModel
{
    private ProjectTechnicalOfficeReportRenderModel(ProjectTechnicalOfficeReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        Documents = Snapshot.Documents.Rows.ToArray();
        Transmittals = Snapshot.Transmittals.Rows.ToArray();
        Rfis = Snapshot.Rfis.Rows.ToArray();
        Submittals = Snapshot.Submittals.Rows.ToArray();
    }

    public ProjectTechnicalOfficeReportRenderRequest Request { get; }
    public ProjectTechnicalOfficeReportSemanticSnapshot Snapshot { get; }
    public IReadOnlyList<TechnicalReportingDocumentRow> Documents { get; }
    public IReadOnlyList<TechnicalReportingTransmittalRow> Transmittals { get; }
    public IReadOnlyList<TechnicalReportingRfiRow> Rfis { get; }
    public IReadOnlyList<TechnicalReportingSubmittalRow> Submittals { get; }
    public int FactCount => Documents.Count + Transmittals.Count + Rfis.Count + Submittals.Count;

    public static ProjectTechnicalOfficeReportRenderModel Create(
        ProjectTechnicalOfficeReportRenderRequest request)
    {
        ProjectTechnicalOfficeReportRenderingContract.ValidateRequest(request);
        return new ProjectTechnicalOfficeReportRenderModel(request);
    }
}

internal static class ProjectTechnicalOfficeReportRenderingContract
{
    public const int MaximumProjectCodeLength = 160;
    public const int MaximumProjectNameLength = 400;
    public const int MaximumNumberLength = 80;
    public const int MaximumDisciplineLength = 160;
    public const int MaximumRevisionCodeLength = 80;

    public static void ValidateRequest(ProjectTechnicalOfficeReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            request.DefinitionCode != ProjectTechnicalOfficeReportRuntimeContract.DefinitionCode ||
            request.DefinitionVersion != ProjectTechnicalOfficeReportRuntimeContract.DefinitionVersion ||
            request.TemplateVersion != ProjectTechnicalOfficeReportRuntimeContract.TemplateVersion ||
            request.TemplateContentDigest != ProjectTechnicalOfficeReportRuntimeContract.TemplateContentDigest ||
            request.RendererContractVersion != ProjectTechnicalOfficeReportRuntimeContract.RendererContractVersion ||
            request.LayoutContractVersion != ProjectTechnicalOfficeReportRuntimeContract.LayoutContractVersion ||
            request.Format is not (ReportFormat.Pdf or ReportFormat.Xlsx) ||
            request.FileName != ReportArtifactIdentity.FileName(request.Snapshot, request.Format) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !IsSha256(request.ManifestSha256) || !IsSha256(request.SnapshotSha256) ||
            !IsSha256(request.SourceManifestSha256) ||
            request.SourceManifestSha256 != request.Snapshot.SourceManifestSha256 ||
            request.SourceCutoffUtc != request.Snapshot.Cutoff.SourceCutoffUtc ||
            CanonicalJson.Sha256(CanonicalJson.Serialize(request.Snapshot)) != request.SnapshotSha256)
        {
            throw new ReportRenderingException(
                "reporting.project_technical_office.render_request.invalid", transient: false,
                "The F07 render request does not match its immutable snapshot and template identity.");
        }
    }

    public static void ValidateSnapshot(ProjectTechnicalOfficeReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != ProjectTechnicalOfficeReportRuntimeContract.SnapshotSchemaVersion ||
            snapshot.SemanticContractId != ProjectTechnicalOfficeReportRuntimeContract.SemanticContractId ||
            snapshot.DefinitionCode != ProjectTechnicalOfficeReportRuntimeContract.DefinitionCode ||
            snapshot.DefinitionVersion != ProjectTechnicalOfficeReportRuntimeContract.DefinitionVersion ||
            snapshot.PolicyVersion != ProjectTechnicalOfficeReportingContract.PolicyVersion ||
            snapshot.DataStatus is not (ReportDataStatus.NotConfigured or ReportDataStatus.NoData or
                ReportDataStatus.InsufficientData or ReportDataStatus.Available) ||
            snapshot.Classification is not (ReportClassification.Confidential or ReportClassification.Restricted) ||
            snapshot.Reasons is null || snapshot.Parameters is null || snapshot.Project is null ||
            snapshot.Cutoff is null || snapshot.Documents is null || snapshot.Transmittals is null ||
            snapshot.Rfis is null || snapshot.Submittals is null ||
            !IsSha256(snapshot.SourceManifestSha256) || !IsSha256(snapshot.SourceSemanticSha256))
        {
            throw InvalidSnapshot("F07 snapshot identity, classification or collections are invalid.");
        }

        var project = snapshot.Project;
        var cutoff = snapshot.Cutoff;
        if (project.Id == Guid.Empty || project.TenantId == Guid.Empty ||
            !HasText(project.Code, MaximumProjectCodeLength) ||
            !HasText(project.Name, MaximumProjectNameLength) ||
            project.Revision <= 0 || project.ConfigurationVersion <= 0 ||
            project.ConfigurationChangedAtUtc == default ||
            project.ConfigurationChangedAtUtc.Offset != TimeSpan.Zero ||
            cutoff.SourceCutoffUtc == default || cutoff.SourceCutoffUtc.Offset != TimeSpan.Zero ||
            project.ConfigurationChangedAtUtc > cutoff.SourceCutoffUtc ||
            cutoff.CutoffLocalDate == default)
        {
            throw InvalidSnapshot("F07 project or cutoff identity is invalid.");
        }
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone);
            if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                cutoff.SourceCutoffUtc, zone).DateTime) != cutoff.CutoffLocalDate)
                throw InvalidSnapshot("F07 local cutoff date contradicts the project time zone.");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or
            InvalidTimeZoneException or ArgumentNullException)
        {
            throw InvalidSnapshot("F07 project time zone is unknown.", exception);
        }

        ValidateReasons(snapshot.Reasons);
        ValidateSection(snapshot.Documents, TechnicalReportingReason.NoOfficialDocumentRevision,
            item => item.Number, ValidateDocument);
        ValidateSection(snapshot.Transmittals, TechnicalReportingReason.NoIssuedTransmittal,
            item => item.Number, ValidateTransmittal);
        ValidateSection(snapshot.Rfis, TechnicalReportingReason.NoIssuedRfi,
            item => item.Number, ValidateRfi);
        ValidateSection(snapshot.Submittals, TechnicalReportingReason.NoSubmittedSubmittal,
            item => item.Number, ValidateSubmittal);
        var sections = new[] { snapshot.Documents.Status, snapshot.Transmittals.Status,
            snapshot.Rfis.Status, snapshot.Submittals.Status };
        var expected = sections.All(item => item == TechnicalReportingStatus.NotConfigured)
            ? ReportDataStatus.NotConfigured
            : sections.Contains(TechnicalReportingStatus.InsufficientData)
                ? ReportDataStatus.InsufficientData
                : sections.All(item => item == TechnicalReportingStatus.NoData)
                    ? ReportDataStatus.NoData : ReportDataStatus.Available;
        if (snapshot.DataStatus != expected ||
            !new[] { snapshot.Documents.Reasons, snapshot.Transmittals.Reasons,
                snapshot.Rfis.Reasons, snapshot.Submittals.Reasons }
                .SelectMany(item => item).All(reason => snapshot.Reasons.Contains(reason)))
            throw InvalidSnapshot("F07 section status or reasons contradict the report status.");
    }

    private static void ValidateSection<T>(
        TechnicalReportingSection<T> section, TechnicalReportingReason noDataReason,
        Func<T, string> number, Action<T> validate)
    {
        if (section.Rows is null || section.Reasons is null ||
            section.Status is not (TechnicalReportingStatus.NotConfigured or
                TechnicalReportingStatus.NoData or TechnicalReportingStatus.InsufficientData or
                TechnicalReportingStatus.Available))
            throw InvalidSnapshot("F07 section status or rows are invalid.");
        ValidateReasons(section.Reasons);
        if (section.Status is TechnicalReportingStatus.NotConfigured or
            TechnicalReportingStatus.InsufficientData)
        {
            if (section.OfficialCount is not null || section.Rows.Count != 0 ||
                (section.Status == TechnicalReportingStatus.InsufficientData &&
                    (!section.Reasons.Contains(TechnicalReportingReason.HistoricalTransitionUnavailable) ||
                     !section.Reasons.Contains(TechnicalReportingReason.SourceCoverageIncomplete))) ||
                (section.Status == TechnicalReportingStatus.NotConfigured &&
                    !section.Reasons.Contains(TechnicalReportingReason.TechnicalSourceNotConfigured)))
                throw InvalidSnapshot("F07 unavailable section must retain null count and explicit reasons.");
        }
        else if (section.OfficialCount != section.Rows.Count ||
            section.Status == TechnicalReportingStatus.NoData &&
                (section.Rows.Count != 0 || !section.Reasons.Contains(noDataReason)) ||
            section.Status == TechnicalReportingStatus.Available && section.Rows.Count == 0)
            throw InvalidSnapshot("F07 official section count or no-data reason is invalid.");
        string? previous = null;
        var ids = new HashSet<Guid>();
        foreach (var item in section.Rows)
        {
            if (item is null || !HasText(number(item), MaximumNumberLength) ||
                previous is not null && StringComparer.Ordinal.Compare(previous, number(item)) >= 0)
                throw InvalidSnapshot("F07 section rows are not canonical.");
            validate(item);
            previous = number(item);
            var id = item switch
            {
                TechnicalReportingDocumentRow row => row.DocumentId,
                TechnicalReportingTransmittalRow row => row.TransmittalId,
                TechnicalReportingRfiRow row => row.RfiId,
                TechnicalReportingSubmittalRow row => row.SubmittalId,
                _ => Guid.Empty
            };
            if (!ids.Add(id)) throw InvalidSnapshot("F07 section identity is duplicated.");
        }
    }

    private static void ValidateDocument(TechnicalReportingDocumentRow row)
    {
        if (row.DocumentId == Guid.Empty || row.RevisionId == Guid.Empty ||
            !Enum.IsDefined(row.Type) || !HasText(row.Discipline, MaximumDisciplineLength) ||
            !HasText(row.RevisionCode, MaximumRevisionCodeLength) ||
            row.RevisionDate == default || !Utc(row.IssuedAtUtc))
            throw InvalidSnapshot("F07 document issue row is invalid.");
    }

    private static void ValidateTransmittal(TechnicalReportingTransmittalRow row)
    {
        if (row.TransmittalId == Guid.Empty || !Utc(row.IssuedAtUtc) ||
            row.RevisionCount <= 0 || !Enum.IsDefined(row.DueState) ||
            row.AcknowledgedAtUtc.HasValue && (!Utc(row.AcknowledgedAtUtc.Value) ||
                row.AcknowledgedAtUtc.Value < row.IssuedAtUtc) ||
            (row.DueState == TechnicalReportingDueState.Acknowledged) !=
                row.AcknowledgedAtUtc.HasValue)
            throw InvalidSnapshot("F07 transmittal issue/acknowledgement row is invalid.");
    }

    private static void ValidateRfi(TechnicalReportingRfiRow row)
    {
        if (row.RfiId == Guid.Empty || !Utc(row.IssuedAtUtc) || row.ResponseCount < 0 ||
            row.State is not (TechnicalReportingEventType.Issued or
                TechnicalReportingEventType.ResponseReceived or
                TechnicalReportingEventType.ResponseAccepted or
                TechnicalReportingEventType.ClarificationRequired or
                TechnicalReportingEventType.Closed) ||
            row.LastResponseClassification.HasValue &&
                !Enum.IsDefined(row.LastResponseClassification.Value) ||
            row.ResponseCount == 0 && row.LastResponseClassification.HasValue ||
            row.State == TechnicalReportingEventType.Closed && row.Overdue != false)
            throw InvalidSnapshot("F07 RFI row is invalid.");
    }

    private static void ValidateSubmittal(TechnicalReportingSubmittalRow row)
    {
        if (row.SubmittalId == Guid.Empty || !Enum.IsDefined(row.Type) ||
            !HasText(row.Discipline, MaximumDisciplineLength) ||
            !Enum.IsDefined(row.State) || row.ResubmissionNumber < 0 ||
            row.ReviewOutcome.HasValue && !Enum.IsDefined(row.ReviewOutcome.Value) ||
            row.State is not (TechnicalReportingSubmittalState.Submitted or
                TechnicalReportingSubmittalState.UnderReview) && row.ReviewOverdue != false)
            throw InvalidSnapshot("F07 Submittal row is invalid.");
    }

    private static void ValidateReasons(IReadOnlyCollection<TechnicalReportingReason> reasons)
    {
        if (reasons.Any(item => !Enum.IsDefined(item)) ||
            !reasons.SequenceEqual(reasons.OrderBy(item => item).Distinct()))
            throw InvalidSnapshot("F07 reasons are invalid or not canonical.");
    }

    private static bool HasText(string? text, int max) =>
        !string.IsNullOrWhiteSpace(text) && text.Trim().Length <= max;
    private static bool Utc(DateTimeOffset at) => at != default && at.Offset == TimeSpan.Zero;
    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(item => char.IsAsciiDigit(item) || item is >= 'a' and <= 'f');

    internal static ReportRenderingException InvalidSnapshot(
        string message, Exception? innerException = null) => new(
        "reporting.project_technical_office.snapshot.payload_invalid", transient: false,
        message, innerException);
}
