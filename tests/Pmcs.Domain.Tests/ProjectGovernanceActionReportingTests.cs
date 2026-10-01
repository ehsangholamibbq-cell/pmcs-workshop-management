using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ActionControl.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectGovernanceActionReportingTests
{
    private static readonly Guid Tenant = Id(1);
    private static readonly Guid Project = Id(2);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 9, 27);
    private static readonly string[] RegisterNames =
    [
        "issues", "risks", "decision_requests", "decisions",
        "escalation_threads", "actions", "risk_matrix_versions", "sla_rule_versions"
    ];

    [Fact]
    public void F09KeepsFiveIndependentStatusesAndNeverPublishesPartialCounts()
    {
        var empty = Empty(GovernanceActionReportingReason.NoOfficialIssue);
        var partial = new GovernanceActionReportingSection(
            GovernanceActionReportingStatus.InsufficientData, null, [],
            [GovernanceActionReportingReason.HistoricalTransitionUnavailable],
            GovernanceActionReportingClassification.Confidential);
        var source = Result(empty, partial, Empty(GovernanceActionReportingReason.NoSubmittedDecision),
            Empty(GovernanceActionReportingReason.NoRaisedEscalation),
            Empty(GovernanceActionReportingReason.NoOfficialAction));
        var snapshot = Build(source);
        Assert.Equal(ReportDataStatus.InsufficientData, snapshot.DataStatus);
        Assert.Contains("insufficientData", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("officialCount\":null", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(source.SourceManifestSha256, snapshot.SourceManifestSha256);
        Assert.Equal(3, ProjectGovernanceActionReportRuntimeContract.RequiredSourcePermissions.Count);
    }

    [Fact]
    public void F09ActionPolicyRaisesClassificationWithoutLeakingSourceOrNarrative()
    {
        var action = new GovernanceActionReportingFact(Id(8), Id(8).ToString("N"),
            GovernanceActionFactKind.Action, "Open", Cutoff.AddDays(-1), Date, null,
            "High", null, GovernanceActionReportingClassification.Restricted);
        var section = new GovernanceActionReportingSection(GovernanceActionReportingStatus.Available,
            1, [action], [], GovernanceActionReportingClassification.Restricted);
        var source = Result(Empty(GovernanceActionReportingReason.NoOfficialIssue),
            Empty(GovernanceActionReportingReason.NoOfficialRisk),
            Empty(GovernanceActionReportingReason.NoSubmittedDecision),
            Empty(GovernanceActionReportingReason.NoRaisedEscalation), section);
        var snapshot = Build(source);
        Assert.Equal(ReportClassification.Restricted, snapshot.Classification);
        Assert.DoesNotContain("SourceFactId", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AssigneeDisplayName", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ProjectGovernanceActionReportingContract.ActionClassificationPolicy,
            snapshot.SourceManifestJson, StringComparison.Ordinal);
    }

    [Fact]
    public void F09RejectsTamperedHashScopeFalseZeroAndActionDowngrade()
    {
        var source = Result(Empty(GovernanceActionReportingReason.NoOfficialIssue),
            Empty(GovernanceActionReportingReason.NoOfficialRisk),
            Empty(GovernanceActionReportingReason.NoSubmittedDecision),
            Empty(GovernanceActionReportingReason.NoRaisedEscalation),
            Empty(GovernanceActionReportingReason.NoOfficialAction));
        Assert.Equal(ReportDataStatus.NoData, Build(source).DataStatus);
        Assert.Throws<DomainRuleException>(() => Build(source with { SemanticSha256 = new string('0', 64) }));
        Assert.Throws<DomainRuleException>(() => Build(source with { ProjectId = Id(3) }));
        var falseZero = source with { Issues = new GovernanceActionReportingSection(
            GovernanceActionReportingStatus.InsufficientData, 0, [],
            [GovernanceActionReportingReason.HistoricalTransitionUnavailable],
            GovernanceActionReportingClassification.Confidential) };
        falseZero = falseZero with { DataStatus = GovernanceActionReportingStatus.InsufficientData,
            Reasons = falseZero.Issues.Reasons.Concat(source.Risks.Reasons)
                .Concat(source.Decisions.Reasons).Concat(source.Escalations.Reasons)
                .Concat(source.Actions.Reasons).Distinct().OrderBy(x => x).ToArray() };
        falseZero = falseZero with { SemanticSha256 = GovernanceActionReportingHash.Result(falseZero) };
        Assert.Throws<DomainRuleException>(() => Build(falseZero));
        var downgraded = new GovernanceActionReportingSection(GovernanceActionReportingStatus.Available,
            1, [new GovernanceActionReportingFact(Id(8), Id(8).ToString("N"),
                GovernanceActionFactKind.Action, "Open", Cutoff.AddDays(-1), Date, null,
                null, null, GovernanceActionReportingClassification.Confidential)], [],
            GovernanceActionReportingClassification.Confidential);
        var unsafeSource = source with { Actions = downgraded, DataStatus = GovernanceActionReportingStatus.Available };
        unsafeSource = unsafeSource with { SemanticSha256 = GovernanceActionReportingHash.Result(unsafeSource) };
        Assert.Throws<DomainRuleException>(() => Build(unsafeSource));
    }

    [Fact]
    public void F09RejectsManifestCountTamperingEvenWithRecomputedDigests()
    {
        var action = new GovernanceActionReportingFact(Id(8), Id(8).ToString("N"),
            GovernanceActionFactKind.Action, "Open", Cutoff.AddDays(-1), Date, null,
            "High", null, GovernanceActionReportingClassification.Restricted);
        var source = Result(Empty(GovernanceActionReportingReason.NoOfficialIssue),
            Empty(GovernanceActionReportingReason.NoOfficialRisk),
            Empty(GovernanceActionReportingReason.NoSubmittedDecision),
            Empty(GovernanceActionReportingReason.NoRaisedEscalation),
            new(GovernanceActionReportingStatus.Available, 1, [action], [],
                GovernanceActionReportingClassification.Restricted));
        var registers = source.SourceManifest.Registers.ToArray();
        registers[5] = registers[5] with { SourceCount = 0 };
        var manifest = source.SourceManifest with { Registers = registers };
        var changed = source with { SourceManifest = manifest,
            SourceManifestSha256 = GovernanceActionReportingHash.Compute(manifest) };
        changed = changed with { SemanticSha256 = GovernanceActionReportingHash.Result(changed) };
        Assert.Throws<DomainRuleException>(() => Build(changed));
    }

    private static GovernanceActionReportingSection Empty(GovernanceActionReportingReason reason) =>
        new(GovernanceActionReportingStatus.NoData, 0, [], [reason],
            GovernanceActionReportingClassification.Confidential);

    private static ReportSnapshot Build(ProjectGovernanceActionReportingResult source) =>
        ProjectGovernanceActionReportSnapshotBuilder.Build(Id(99), Tenant, Profile(), Cutoff,
            source, Cutoff.AddMinutes(1), Cutoff.AddMinutes(2));

    private static ProjectGovernanceActionReportingResult Result(
        GovernanceActionReportingSection issues, GovernanceActionReportingSection risks,
        GovernanceActionReportingSection decisions, GovernanceActionReportingSection escalations,
        GovernanceActionReportingSection actions)
    {
        var sections = new[] { issues, risks, decisions, escalations, actions };
        var classification = sections.Any(x => x.Classification == GovernanceActionReportingClassification.Restricted)
            ? GovernanceActionReportingClassification.Restricted : GovernanceActionReportingClassification.Confidential;
        var status = sections.Any(x => x.Status == GovernanceActionReportingStatus.InsufficientData)
            ? GovernanceActionReportingStatus.InsufficientData
            : sections.Any(x => x.Status == GovernanceActionReportingStatus.Available)
                ? GovernanceActionReportingStatus.Available : GovernanceActionReportingStatus.NoData;
        var counts = new[] { issues.Rows.Count, risks.Rows.Count,
            decisions.Rows.Count(x => x.Kind == GovernanceActionFactKind.DecisionRequest),
            decisions.Rows.Count(x => x.Kind == GovernanceActionFactKind.DecisionRecord),
            escalations.Rows.Count, actions.Rows.Count, 0, 0 };
        var registers = RegisterNames.Select((name, index) => new GovernanceActionReportingRegister(
            name, counts[index], GovernanceActionReportingHash.Compute(Array.Empty<Guid>()))).ToArray();
        var manifest = new ProjectGovernanceActionSourceManifest(
            ProjectGovernanceActionReportingContract.ManifestVersion,
            ProjectGovernanceActionReportingContract.Version,
            ProjectGovernanceActionReportingContract.PolicyVersion,
            ProjectGovernanceActionReportingContract.ActionClassificationPolicy,
            Tenant, Project, Date, Cutoff, Cutoff, 1, Cutoff.AddDays(-30),
            classification, registers);
        var result = new ProjectGovernanceActionReportingResult(
            ProjectGovernanceActionReportingContract.Version,
            ProjectGovernanceActionReportingContract.PolicyVersion,
            Tenant, Project, Date, Cutoff, classification, status,
            sections.SelectMany(x => x.Reasons).Distinct().OrderBy(x => x).ToArray(),
            issues, risks, decisions, escalations, actions, manifest,
            GovernanceActionReportingHash.Compute(manifest), "");
        return result with { SemanticSha256 = GovernanceActionReportingHash.Result(result) };
    }

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static ProjectControlProfile Profile() => new(
        Project, Tenant, "P-001", "Project", "UTC", "IRR", 1,
        Cutoff.AddDays(-30), ProjectStatus.Active, ContractModel.NotConfigured,
        PlanningMode.None, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured, ProjectFeatureState.NotConfigured,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null), 1);
}
