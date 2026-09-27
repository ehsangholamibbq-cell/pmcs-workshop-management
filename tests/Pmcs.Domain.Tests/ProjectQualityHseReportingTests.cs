using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.QualitySafety.Contracts;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectQualityHseReportingTests
{
    private static readonly Guid Tenant = Id(1);
    private static readonly Guid Project = Id(2);
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 9, 27);
    private static readonly string[] RegisterNames =
    [
        "intakes", "inspections", "nonconformances", "defects", "incidents",
        "corrective_actions", "permits", "toolbox_talks", "inspection_test_plan_versions",
        "checklist_template_versions", "test_records", "competency_records",
        "exposure_hours", "risk_matrix_versions"
    ];

    [Fact]
    public void F08IndependentNoDataAndIncompleteSectionsNeverPublishPartialCount()
    {
        var quality = new QualityHseReportingSection(QualityHseReportingStatus.NoData, 0, [],
            [QualityHseReportingReason.NoOfficialQualityFact], QualityHseReportingClassification.Confidential);
        var hse = new QualityHseReportingSection(QualityHseReportingStatus.InsufficientData, null, [],
            [QualityHseReportingReason.HistoricalTransitionUnavailable], QualityHseReportingClassification.Confidential);
        var result = Result(quality, hse);
        var snapshot = Build(result);
        Assert.Equal(ReportDataStatus.InsufficientData, snapshot.DataStatus);
        Assert.Contains("insufficientData", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("personalMedical", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(result.SourceManifestSha256, snapshot.SourceManifestSha256);
        Assert.Equal(3, ProjectQualityHseReportRuntimeContract.RequiredSourcePermissions.Count);
    }

    [Fact]
    public void F08RejectsTamperedDigestScopeAndUnsupportedClassification()
    {
        var empty = new QualityHseReportingSection(QualityHseReportingStatus.NoData, 0, [],
            [QualityHseReportingReason.NoOfficialQualityFact], QualityHseReportingClassification.Confidential);
        var result = Result(empty, empty);
        Assert.Equal(ReportDataStatus.NoData, Build(result).DataStatus);
        Assert.Throws<DomainRuleException>(() => Build(result with { SemanticSha256 = new string('0', 64) }));
        Assert.Throws<DomainRuleException>(() => Build(result with { ProjectId = Id(3) }));
        Assert.Throws<DomainRuleException>(() => Build(result with
        {
            Classification = (QualityHseReportingClassification)99
        }));
    }

    private static ReportSnapshot Build(ProjectQualityHseReportingResult source) =>
        ProjectQualityHseReportSnapshotBuilder.Build(Id(99), Tenant, Profile(), Cutoff,
            source, Cutoff.AddMinutes(1), Cutoff.AddMinutes(2));

    private static ProjectQualityHseReportingResult Result(
        QualityHseReportingSection quality, QualityHseReportingSection hse)
    {
        var registers = RegisterNames.Select(name => new QualityHseReportingRegister(
            name, 0, QualityHseReportingHash.Compute(Array.Empty<Guid>()))).ToArray();
        var manifest = new ProjectQualityHseSourceManifest(
            ProjectQualityHseReportingContract.ManifestVersion, ProjectQualityHseReportingContract.Version,
            ProjectQualityHseReportingContract.PolicyVersion, Tenant, Project, Date,
            Cutoff, Cutoff, 1, Cutoff.AddDays(-30), null, null,
            null, null, false, false, null, null, false, false,
            QualityHseReportingClassification.Confidential, registers);
        var status = quality.Status == QualityHseReportingStatus.InsufficientData ||
            hse.Status == QualityHseReportingStatus.InsufficientData
                ? QualityHseReportingStatus.InsufficientData : QualityHseReportingStatus.NoData;
        var result = new ProjectQualityHseReportingResult(
            ProjectQualityHseReportingContract.Version, ProjectQualityHseReportingContract.PolicyVersion,
            Tenant, Project, Date, Cutoff, QualityHseReportingClassification.Confidential,
            status, quality.Reasons.Concat(hse.Reasons).Distinct().OrderBy(x => x).ToArray(),
            quality, hse, manifest, QualityHseReportingHash.Compute(manifest), "");
        return result with { SemanticSha256 = QualityHseReportingHash.Result(result) };
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
