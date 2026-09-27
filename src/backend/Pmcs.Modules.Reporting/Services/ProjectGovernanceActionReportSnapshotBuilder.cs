using System.Text;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectGovernanceActionReportSnapshotBuilder
{
    private static readonly string[] RegisterNames =
    [
        "issues", "risks", "decision_requests", "decisions",
        "escalation_threads", "actions", "risk_matrix_versions", "sla_rule_versions"
    ];

    public static ReportSnapshot Build(Guid runId, Guid tenantId, ProjectControlProfile project,
        DateTimeOffset cutoffUtc, ProjectGovernanceActionReportingResult source,
        DateTimeOffset acceptedAtUtc, DateTimeOffset builtAt) => Build(runId, tenantId,
        ProjectGovernanceActionPinnedProjectProfile.Capture(project, acceptedAtUtc),
        cutoffUtc, source, acceptedAtUtc, builtAt);

    public static ReportSnapshot Build(Guid runId, Guid tenantId,
        ProjectGovernanceActionPinnedProjectProfile project, DateTimeOffset cutoffUtc,
        ProjectGovernanceActionReportingResult source, DateTimeOffset acceptedAtUtc,
        DateTimeOffset builtAt)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        var cutoff = cutoffUtc.ToUniversalTime();
        var zone = project.ValidateForRun(tenantId, project.Id, cutoff, acceptedAtUtc);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cutoff, zone).DateTime);
        Validate(source, tenantId, project, cutoff, date);
        var classification = MapClassification(source.Classification);
        var payload = new ProjectGovernanceActionReportSemanticSnapshot(
            ProjectGovernanceActionReportRuntimeContract.SnapshotSchemaVersion,
            ProjectGovernanceActionReportRuntimeContract.SemanticContractId,
            ProjectGovernanceActionReportRuntimeContract.DefinitionCode,
            ProjectGovernanceActionReportRuntimeContract.DefinitionVersion, source.PolicyVersion,
            MapStatus(source.DataStatus), source.Reasons,
            new ProjectGovernanceActionReportParameters(),
            new ProjectGovernanceActionReportProjectIdentity(project.Id, project.TenantId,
                project.Code, project.Name, project.TimeZone, project.Revision,
                project.ConfigurationVersion, project.ConfigurationChangedAtUtc,
                project.CapturedAtUtc),
            new ProjectGovernanceActionReportCutoffIdentity(cutoff, date), classification,
            Section(source.Issues), Section(source.Risks), Section(source.Decisions),
            Section(source.Escalations), Section(source.Actions),
            source.SourceManifestSha256, source.SemanticSha256);
        var payloadJson = CanonicalJson.Serialize(payload);
        var manifestJson = CanonicalJson.Serialize(source.SourceManifest);
        if (Encoding.UTF8.GetByteCount(payloadJson) + Encoding.UTF8.GetByteCount(manifestJson) >
            ProjectGovernanceActionReportingContract.MaximumSnapshotBytes)
            throw Invalid("snapshot.overflow", "Governance/Action snapshot byte budget exceeded.");
        return ReportSnapshot.Create(Guid.NewGuid(), runId, tenantId, project.Id,
            ProjectGovernanceActionReportRuntimeContract.SnapshotSchemaVersion,
            MapStatus(source.DataStatus), payloadJson, manifestJson, classification, builtAt, cutoff);
    }

    private static void Validate(ProjectGovernanceActionReportingResult source, Guid tenantId,
        ProjectGovernanceActionPinnedProjectProfile project, DateTimeOffset cutoff, DateOnly date)
    {
        var sections = new[] { source.Issues, source.Risks, source.Decisions,
            source.Escalations, source.Actions };
        if (source.ContractVersion != ProjectGovernanceActionReportingContract.Version ||
            source.PolicyVersion != ProjectGovernanceActionReportingContract.PolicyVersion ||
            source.TenantId != tenantId || source.ProjectId != project.Id ||
            source.CutoffLocalDate != date || source.SourceCutoffUtc.ToUniversalTime() != cutoff ||
            source.SourceManifest is null || source.Reasons is null ||
            sections.Any(x => x is null) || !Enum.IsDefined(source.DataStatus) ||
            !Enum.IsDefined(source.Classification) ||
            source.Classification < GovernanceActionReportingClassification.Confidential ||
            source.DataStatus != AggregateStatus(sections.Select(x => x.Status).ToArray()) ||
            !source.Reasons.SequenceEqual(sections.SelectMany(x => x.Reasons)
                .Distinct().OrderBy(x => x)) ||
            source.Reasons.Any(x => !Enum.IsDefined(x)) ||
            !ValidSection(source.Issues, cutoff, GovernanceActionFactKind.Issue) ||
            !ValidSection(source.Risks, cutoff, GovernanceActionFactKind.Risk) ||
            !ValidSection(source.Decisions, cutoff, GovernanceActionFactKind.DecisionRequest,
                GovernanceActionFactKind.DecisionRecord) ||
            !ValidSection(source.Escalations, cutoff, GovernanceActionFactKind.Escalation) ||
            !ValidSection(source.Actions, cutoff, GovernanceActionFactKind.Action) ||
            source.Actions.Rows.Any(x => x.Classification != GovernanceActionReportingClassification.Restricted) ||
            sections.SelectMany(x => x.Rows).Select(x => x.Id).Distinct().Count() !=
                sections.Sum(x => x.Rows.Count) ||
            sections.Sum(x => x.Rows.Count) > ProjectGovernanceActionReportingContract.MaximumFacts ||
            source.Classification != (sections.Any(x => x.Classification ==
                GovernanceActionReportingClassification.Restricted)
                ? GovernanceActionReportingClassification.Restricted
                : GovernanceActionReportingClassification.Confidential) ||
            source.SourceManifestSha256 != GovernanceActionReportingHash.Compute(source.SourceManifest) ||
            source.SemanticSha256 != GovernanceActionReportingHash.Result(source))
            throw Invalid("source.invalid", "Governance/Action source content or semantic digest is invalid.");

        var manifest = source.SourceManifest;
        if (manifest.ManifestVersion != ProjectGovernanceActionReportingContract.ManifestVersion ||
            manifest.ContractVersion != source.ContractVersion ||
            manifest.PolicyVersion != source.PolicyVersion ||
            manifest.ActionClassificationPolicy != ProjectGovernanceActionReportingContract.ActionClassificationPolicy ||
            manifest.TenantId != tenantId || manifest.ProjectId != project.Id ||
            manifest.CutoffLocalDate != date || manifest.SourceCutoffUtc.ToUniversalTime() != cutoff ||
            manifest.WatermarkUtc.ToUniversalTime() != cutoff ||
            manifest.ProjectConfigurationVersion != project.ConfigurationVersion ||
            manifest.ProjectConfigurationChangedAtUtc.ToUniversalTime() != project.ConfigurationChangedAtUtc ||
            manifest.Classification != source.Classification || manifest.Registers is null ||
            manifest.Registers.Count != RegisterNames.Length ||
            !manifest.Registers.Select(x => x.Name).SequenceEqual(RegisterNames) ||
            manifest.Registers.Any(x => x.SourceCount < 0 ||
                x.SourceCount > ProjectGovernanceActionReportingContract.MaximumPerRegister ||
                !IsDigest(x.FactSha256)) ||
            !CountMatches(source.Issues, manifest.Registers.ElementAt(0).SourceCount) ||
            !CountMatches(source.Risks, manifest.Registers.ElementAt(1).SourceCount) ||
            !CountMatches(source.Escalations, manifest.Registers.ElementAt(4).SourceCount) ||
            !CountMatches(source.Actions, manifest.Registers.ElementAt(5).SourceCount) ||
            source.Decisions.Rows.Count(x => x.Kind == GovernanceActionFactKind.DecisionRequest) >
                manifest.Registers.ElementAt(2).SourceCount ||
            source.Decisions.Rows.Count(x => x.Kind == GovernanceActionFactKind.DecisionRecord) >
                manifest.Registers.ElementAt(3).SourceCount ||
            source.Decisions.Status != GovernanceActionReportingStatus.InsufficientData &&
                source.Decisions.Rows.Count(x => x.Kind == GovernanceActionFactKind.DecisionRecord) !=
                manifest.Registers.ElementAt(3).SourceCount ||
            !IsDigest(source.SourceManifestSha256) || !IsDigest(source.SemanticSha256))
            throw Invalid("source_manifest.invalid", "Governance/Action manifest conflicts with the pinned project scope.");
    }

    private static bool CountMatches(GovernanceActionReportingSection section, int sourceCount) =>
        section.Status == GovernanceActionReportingStatus.InsufficientData ||
        section.OfficialCount == sourceCount;

    private static bool ValidSection(GovernanceActionReportingSection section,
        DateTimeOffset cutoff, params GovernanceActionFactKind[] allowedKinds)
    {
        if (!Enum.IsDefined(section.Status) || !Enum.IsDefined(section.Classification) ||
            section.Classification < GovernanceActionReportingClassification.Confidential ||
            section.Rows is null || section.Reasons is null ||
            section.Reasons.Any(x => !Enum.IsDefined(x)) ||
            section.Reasons.Distinct().Count() != section.Reasons.Count ||
            section.Rows.Count > ProjectGovernanceActionReportingContract.MaximumFacts ||
            section.Rows.Select(x => x.Id).Distinct().Count() != section.Rows.Count ||
            !section.Rows.SequenceEqual(section.Rows.OrderBy(x => x.Kind)
                .ThenBy(x => x.Number, StringComparer.Ordinal).ThenBy(x => x.Id)) ||
            section.Rows.Any(x => x.Id == Guid.Empty || string.IsNullOrWhiteSpace(x.Number) ||
                !allowedKinds.Contains(x.Kind) || string.IsNullOrWhiteSpace(x.State) ||
                x.OfficialAtUtc.ToUniversalTime() > cutoff ||
                !Enum.IsDefined(x.Classification) ||
                x.Classification < GovernanceActionReportingClassification.Confidential ||
                x.Classification > section.Classification || x.MatrixVersion < 1))
            return false;
        return section.Status switch
        {
            GovernanceActionReportingStatus.Available => section.OfficialCount == section.Rows.Count &&
                section.Rows.Count > 0,
            GovernanceActionReportingStatus.NoData => section.OfficialCount == 0 &&
                section.Rows.Count == 0 && section.Reasons.Count > 0,
            GovernanceActionReportingStatus.NotConfigured or GovernanceActionReportingStatus.InsufficientData =>
                section.OfficialCount is null && section.Rows.Count == 0 && section.Reasons.Count > 0,
            _ => false
        };
    }

    private static GovernanceActionReportingStatus AggregateStatus(
        IReadOnlyCollection<GovernanceActionReportingStatus> sections)
    {
        if (sections.Contains(GovernanceActionReportingStatus.InsufficientData))
            return GovernanceActionReportingStatus.InsufficientData;
        if (sections.Contains(GovernanceActionReportingStatus.Available))
            return GovernanceActionReportingStatus.Available;
        return sections.All(x => x == GovernanceActionReportingStatus.NotConfigured)
            ? GovernanceActionReportingStatus.NotConfigured : GovernanceActionReportingStatus.NoData;
    }

    private static bool IsDigest(string value) => value is { Length: 64 } &&
        value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static ProjectGovernanceActionReportSection Section(GovernanceActionReportingSection section) =>
        new(section.Status, section.OfficialCount, section.Rows,
            section.Reasons, MapClassification(section.Classification));

    private static ReportDataStatus MapStatus(GovernanceActionReportingStatus status) => status switch
    {
        GovernanceActionReportingStatus.NotConfigured => ReportDataStatus.NotConfigured,
        GovernanceActionReportingStatus.NoData => ReportDataStatus.NoData,
        GovernanceActionReportingStatus.InsufficientData => ReportDataStatus.InsufficientData,
        GovernanceActionReportingStatus.Available => ReportDataStatus.Available,
        _ => throw Invalid("status.invalid", "Unknown Governance/Action status.")
    };

    private static ReportClassification MapClassification(
        GovernanceActionReportingClassification classification) => classification switch
        {
            GovernanceActionReportingClassification.Confidential => ReportClassification.Confidential,
            GovernanceActionReportingClassification.Restricted => ReportClassification.Restricted,
            _ => throw Invalid("classification.invalid", "Unknown Governance/Action classification.")
        };

    private static DomainRuleException Invalid(string code, string message) =>
        new($"reporting.project_governance_action.{code}", message);
}
