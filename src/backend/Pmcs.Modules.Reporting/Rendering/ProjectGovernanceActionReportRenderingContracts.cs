using System.Text.Json;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class ProjectGovernanceActionReportRenderSnapshot
{
    public static ProjectGovernanceActionReportSemanticSnapshot Parse(string payloadJson)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ProjectGovernanceActionReportSemanticSnapshot>(
                payloadJson, CanonicalJson.SerializerOptions)
                ?? throw ProjectGovernanceActionReportRenderingContract.InvalidSnapshot("F09 snapshot is empty.");
            ProjectGovernanceActionReportRenderingContract.ValidateSnapshot(snapshot);
            return snapshot;
        }
        catch (ReportRenderingException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw ProjectGovernanceActionReportRenderingContract.InvalidSnapshot(
                "F09 snapshot cannot be rendered.", exception);
        }
    }
}

internal sealed record ProjectGovernanceActionReportRenderRequest(
    Guid RunId, Guid OutputId, Guid SnapshotId, Guid TemplateVersionId,
    string DefinitionCode, string DefinitionVersion, string TemplateVersion,
    string TemplateContentDigest, string RendererContractVersion,
    string LayoutContractVersion, ReportFormat Format, string FileName,
    string VerificationCode, string ManifestSha256, string SnapshotSha256,
    string SourceManifestSha256, DateTimeOffset SourceCutoffUtc,
    ProjectGovernanceActionReportSemanticSnapshot Snapshot);

internal interface IProjectGovernanceActionReportRenderer
{
    ReportFormat Format { get; }
    RenderedReportArtifact Render(ProjectGovernanceActionReportRenderRequest request);
}

internal sealed class ProjectGovernanceActionReportRenderModel
{
    private ProjectGovernanceActionReportRenderModel(ProjectGovernanceActionReportRenderRequest request)
    {
        Request = request;
        Snapshot = request.Snapshot;
        Sections =
        [
            new("Issue", "مسئله", Snapshot.Issues),
            new("Risk", "ریسک", Snapshot.Risks),
            new("Decision", "تصمیم", Snapshot.Decisions),
            new("Escalation", "ارجاع", Snapshot.Escalations),
            new("Action", "اقدام", Snapshot.Actions)
        ];
    }

    public ProjectGovernanceActionReportRenderRequest Request { get; }
    public ProjectGovernanceActionReportSemanticSnapshot Snapshot { get; }
    public IReadOnlyList<GovernanceActionRenderSection> Sections { get; }
    public int FactCount => Sections.Sum(x => x.Section.Rows.Count);

    public static ProjectGovernanceActionReportRenderModel Create(
        ProjectGovernanceActionReportRenderRequest request)
    {
        ProjectGovernanceActionReportRenderingContract.ValidateRequest(request);
        return new(request);
    }
}

internal sealed record GovernanceActionRenderSection(
    string SheetName, string Title, ProjectGovernanceActionReportSection Section);

internal static class ProjectGovernanceActionReportRenderingContract
{
    public static void ValidateRequest(ProjectGovernanceActionReportRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSnapshot(request.Snapshot);
        if (request.RunId == Guid.Empty || request.OutputId == Guid.Empty ||
            request.SnapshotId == Guid.Empty || request.TemplateVersionId == Guid.Empty ||
            request.DefinitionCode != ProjectGovernanceActionReportRuntimeContract.DefinitionCode ||
            request.DefinitionVersion != ProjectGovernanceActionReportRuntimeContract.DefinitionVersion ||
            request.TemplateVersion != ProjectGovernanceActionReportRuntimeContract.TemplateVersion ||
            request.TemplateContentDigest != ProjectGovernanceActionReportRuntimeContract.TemplateContentDigest ||
            request.RendererContractVersion != ProjectGovernanceActionReportRuntimeContract.RendererContractVersion ||
            request.LayoutContractVersion != ProjectGovernanceActionReportRuntimeContract.LayoutContractVersion ||
            request.Format is not (ReportFormat.Pdf or ReportFormat.Xlsx) ||
            request.FileName != ReportArtifactIdentity.FileName(request.Snapshot, request.Format) ||
            string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !Digest(request.ManifestSha256) || !Digest(request.SnapshotSha256) ||
            request.SourceManifestSha256 != request.Snapshot.SourceManifestSha256 ||
            request.SourceCutoffUtc.ToUniversalTime() != request.Snapshot.Cutoff.SourceCutoffUtc ||
            CanonicalJson.Sha256(CanonicalJson.Serialize(request.Snapshot)) != request.SnapshotSha256)
            throw new ReportRenderingException("reporting.project_governance_action.render_request.invalid",
                transient: false, "F09 render identity differs from its immutable snapshot or template.");
    }

    public static void ValidateSnapshot(ProjectGovernanceActionReportSemanticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != ProjectGovernanceActionReportRuntimeContract.SnapshotSchemaVersion ||
            snapshot.SemanticContractId != ProjectGovernanceActionReportRuntimeContract.SemanticContractId ||
            snapshot.DefinitionCode != ProjectGovernanceActionReportRuntimeContract.DefinitionCode ||
            snapshot.DefinitionVersion != ProjectGovernanceActionReportRuntimeContract.DefinitionVersion ||
            snapshot.PolicyVersion != ProjectGovernanceActionReportingContract.PolicyVersion ||
            snapshot.DataStatus is not (ReportDataStatus.NotConfigured or ReportDataStatus.NoData or
                ReportDataStatus.InsufficientData or ReportDataStatus.Available) ||
            snapshot.Classification is not (ReportClassification.Confidential or ReportClassification.Restricted) ||
            snapshot.Reasons is null || snapshot.Parameters is null || snapshot.Project is null ||
            snapshot.Cutoff is null || snapshot.Issues is null || snapshot.Risks is null ||
            snapshot.Decisions is null || snapshot.Escalations is null || snapshot.Actions is null ||
            !Digest(snapshot.SourceManifestSha256) || !Digest(snapshot.SemanticSha256))
            throw InvalidSnapshot("F09 snapshot identity, classification or sections are invalid.");

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
            cutoff.CutoffLocalDate == default)
            throw InvalidSnapshot("F09 pinned project or cutoff is invalid.");
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(project.TimeZone);
            if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff.SourceCutoffUtc, zone).DateTime) !=
                cutoff.CutoffLocalDate)
                throw InvalidSnapshot("F09 project local cutoff contradicts its time zone.");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or
            InvalidTimeZoneException or ArgumentNullException)
        {
            throw InvalidSnapshot("F09 project time zone is unknown.", exception);
        }

        ValidateSection(snapshot.Issues, cutoff.SourceCutoffUtc, GovernanceActionFactKind.Issue);
        ValidateSection(snapshot.Risks, cutoff.SourceCutoffUtc, GovernanceActionFactKind.Risk);
        ValidateSection(snapshot.Decisions, cutoff.SourceCutoffUtc,
            GovernanceActionFactKind.DecisionRequest, GovernanceActionFactKind.DecisionRecord);
        ValidateSection(snapshot.Escalations, cutoff.SourceCutoffUtc, GovernanceActionFactKind.Escalation);
        ValidateSection(snapshot.Actions, cutoff.SourceCutoffUtc, GovernanceActionFactKind.Action);
        var sections = new[] { snapshot.Issues, snapshot.Risks, snapshot.Decisions,
            snapshot.Escalations, snapshot.Actions };
        var statuses = sections.Select(x => x.Status).ToArray();
        var expected = statuses.Contains(GovernanceActionReportingStatus.InsufficientData)
            ? ReportDataStatus.InsufficientData
            : statuses.Contains(GovernanceActionReportingStatus.Available)
                ? ReportDataStatus.Available
                : statuses.All(x => x == GovernanceActionReportingStatus.NotConfigured)
                    ? ReportDataStatus.NotConfigured : ReportDataStatus.NoData;
        if (snapshot.DataStatus != expected ||
            snapshot.Classification != (sections.Any(x => x.Classification == ReportClassification.Restricted)
                ? ReportClassification.Restricted : ReportClassification.Confidential) ||
            sections.Sum(x => x.Rows.Count) > ProjectGovernanceActionReportingContract.MaximumFacts ||
            sections.SelectMany(x => x.Rows).Select(x => x.Id).Distinct().Count() !=
                sections.Sum(x => x.Rows.Count) ||
            !snapshot.Reasons.SequenceEqual(sections.SelectMany(x => x.Reasons)
                .Distinct().OrderBy(x => x)))
            throw InvalidSnapshot("F09 status, classification or reasons contradict its sections.");
    }

    private static void ValidateSection(ProjectGovernanceActionReportSection section,
        DateTimeOffset cutoff, params GovernanceActionFactKind[] allowed)
    {
        if (!Enum.IsDefined(section.Status) ||
            section.Classification is not (ReportClassification.Confidential or ReportClassification.Restricted) ||
            section.Rows is null || section.Reasons is null ||
            section.Reasons.Any(x => !Enum.IsDefined(x)) ||
            section.Reasons.Distinct().Count() != section.Reasons.Count ||
            section.Rows.Count > ProjectGovernanceActionReportingContract.MaximumFacts)
            throw InvalidSnapshot("F09 section status, reasons or row budget is invalid.");
        if (section.Status is GovernanceActionReportingStatus.NotConfigured or
            GovernanceActionReportingStatus.InsufficientData)
        {
            if (section.OfficialCount is not null || section.Rows.Count != 0 || section.Reasons.Count == 0)
                throw InvalidSnapshot("F09 unavailable section cannot publish a partial count.");
        }
        else if (section.OfficialCount != section.Rows.Count ||
            section.Status == GovernanceActionReportingStatus.NoData && section.Rows.Count != 0 ||
            section.Status == GovernanceActionReportingStatus.Available && section.Rows.Count == 0)
            throw InvalidSnapshot("F09 official section count is invalid.");

        GovernanceActionReportingFact? prior = null;
        var ids = new HashSet<Guid>();
        foreach (var row in section.Rows)
        {
            if (row is null || row.Id == Guid.Empty || !ids.Add(row.Id) ||
                string.IsNullOrWhiteSpace(row.Number) || row.Number.Length > 80 ||
                row.OfficialAtUtc == default || row.OfficialAtUtc.Offset != TimeSpan.Zero ||
                row.OfficialAtUtc > cutoff ||
                row.DueLocalDate == default(DateOnly) ||
                row.SlaDueAtUtc.HasValue && row.SlaDueAtUtc.Value.Offset != TimeSpan.Zero ||
                row.MatrixVersion < 1 || !allowed.Contains(row.Kind) ||
                !ValidState(row) ||
                row.Classification is not (GovernanceActionReportingClassification.Confidential or
                    GovernanceActionReportingClassification.Restricted) ||
                (int)row.Classification > (int)section.Classification ||
                row.Kind == GovernanceActionFactKind.Action &&
                    row.Classification != GovernanceActionReportingClassification.Restricted ||
                prior is not null && (prior.Kind > row.Kind || prior.Kind == row.Kind &&
                    (string.CompareOrdinal(prior.Number, row.Number) > 0 ||
                     prior.Number == row.Number && prior.Id.CompareTo(row.Id) >= 0)))
                throw InvalidSnapshot("F09 section contains an invalid or noncanonical fact.");
            prior = row;
        }
    }

    private static bool ValidState(GovernanceActionReportingFact row) => row.Kind switch
    {
        GovernanceActionFactKind.Issue => new[] { "Open", "UnderAssessment", "ResponseInProgress",
            "PendingVerification", "Resolved", "Closed", "Reopened", "NotAnIssue", "Void" }.Contains(row.State),
        GovernanceActionFactKind.Risk => new[] { "Proposed", "Assessed", "Active", "Monitoring",
            "Materialized", "Expired", "Closed", "Reopened" }.Contains(row.State),
        GovernanceActionFactKind.DecisionRequest => new[] { "ReadyForDecision", "InDecision",
            "MoreInformationRequired", "Decided", "Implementing", "EffectReviewed", "Closed",
            "Withdrawn" }.Contains(row.State),
        GovernanceActionFactKind.DecisionRecord => new[] { "Recorded", "EffectReviewed",
            "Superseded" }.Contains(row.State),
        GovernanceActionFactKind.Escalation => new[] { "Open", "Acknowledged",
            "ClosedBySourceResolution" }.Contains(row.State),
        GovernanceActionFactKind.Action => new[] { "Open", "InProgress", "Blocked", "Done",
            "Cancelled" }.Contains(row.State),
        _ => false
    };

    private static bool Digest(string? value) => value is { Length: 64 } &&
        value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static ReportRenderingException InvalidSnapshot(string message, Exception? inner = null) =>
        new("reporting.project_governance_action.snapshot.payload_invalid", transient: false,
            message, inner);
}
