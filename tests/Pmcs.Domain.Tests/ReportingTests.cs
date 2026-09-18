using Pmcs.BuildingBlocks.Domain;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.IdentityAccess.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Endpoints;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ReportingTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DescriptorPublishesCertifiedReportingAndReadOnlyAgentContracts()
    {
        var descriptor = new ReportingModule().Descriptor;

        Assert.Equal("reporting.center", descriptor.ModuleId);
        Assert.False(descriptor.IsLegacy);
        Assert.Contains("projects.core", descriptor.Dependencies);
        Assert.Contains("legacy.fieldoperations", descriptor.Dependencies);
        Assert.Contains(descriptor.Permissions, item => item.Key == "reporting.catalog.read");
        Assert.Contains(descriptor.Permissions, item => item.Key == "reporting.run.create");
        Assert.Contains(descriptor.Permissions, item => item.Key == "reporting.output.download");
        Assert.Contains(descriptor.Permissions, item =>
            item.Key == "reporting.template.publish" && item.RiskClass == ManifestRiskClass.High);
        Assert.Contains(descriptor.NavigationItems, item =>
            item.FeatureFlag == "reporting.phase1" && item.Permission == "reporting.catalog.read");
        Assert.All(descriptor.Tools, item => Assert.Equal(ToolAccessMode.ReadOnly, item.AccessMode));
        Assert.Contains(descriptor.Tools, item => item.Id == "reporting.catalog.list");
        Assert.Contains(descriptor.Tools, item => item.Id == "reporting.runs.get");
        Assert.Contains(descriptor.Tools, item => item.Id == "reporting.outputs.describe");
        Assert.Contains(descriptor.Events, item =>
            item.Name == "reporting.report.completed" && item.Version == 1);
    }

    [Fact]
    public void CanonicalJsonSortsObjectPropertiesAndProducesStableLowercaseHash()
    {
        using var first = System.Text.Json.JsonDocument.Parse("{\"z\":2,\"a\":{\"d\":4,\"b\":3}}");
        using var second = System.Text.Json.JsonDocument.Parse("{\"a\":{\"b\":3,\"d\":4},\"z\":2}");

        var firstJson = CanonicalJson.Normalize(first.RootElement);
        var secondJson = CanonicalJson.Normalize(second.RootElement);

        Assert.Equal("{\"a\":{\"b\":3,\"d\":4},\"z\":2}", firstJson);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(CanonicalJson.Sha256(firstJson), CanonicalJson.Sha256(secondJson));
        Assert.Matches("^[0-9a-f]{64}$", CanonicalJson.Sha256(firstJson));
    }

    [Fact]
    public void RunTransitionsFromQueueToImmutableSnapshotReadyStage()
    {
        var run = CreateRun();

        run.StartAttempt("{\"allowed\":true}", Cutoff.AddSeconds(1));
        run.AttachSnapshot(Guid.NewGuid(), "{\"allowed\":true}", Cutoff.AddSeconds(2));

        Assert.Equal(ReportRunStatus.Processing, run.Status);
        Assert.Equal(ReportPipelineStage.SnapshotReady, run.PipelineStage);
        Assert.Equal(1, run.AttemptCount);
        Assert.NotNull(run.SnapshotId);
        Assert.Equal(3, run.Revision);
        Assert.Equal(
            "reporting.run.invalid_state",
            Assert.Throws<DomainRuleException>(() =>
                run.AttachSnapshot(Guid.NewGuid(), "{\"allowed\":true}", Cutoff.AddSeconds(3))).Code);
    }

    [Fact]
    public void OfficialVersionRuleHonorsApprovalAndSupersessionCutoff()
    {
        var approvedAt = Cutoff.AddHours(-2);
        var supersededAt = Cutoff.AddHours(1);

        Assert.False(DailyReportReportingRules.IsOfficialAt(approvedAt, supersededAt, approvedAt.AddTicks(-1)));
        Assert.True(DailyReportReportingRules.IsOfficialAt(approvedAt, supersededAt, Cutoff));
        Assert.False(DailyReportReportingRules.IsOfficialAt(approvedAt, supersededAt, supersededAt));
        Assert.True(DailyReportReportingRules.IsOfficialAt(approvedAt, null, Cutoff.AddYears(1)));
    }

    [Fact]
    public void SemanticSnapshotHashIsIndependentOfBuildTimeAndPreservesLineage()
    {
        var tenantId = Guid.NewGuid();
        var project = Project(tenantId);
        var reportId = Guid.NewGuid();
        var chain = Chain(reportId);
        var parameters = new DailyReportReportParameters(reportId, true);
        var builder = new DailyReportSnapshotBuilder();

        var first = builder.Build(
            Guid.NewGuid(), tenantId, project, Cutoff, parameters, chain, Cutoff.AddMinutes(1));
        var second = builder.Build(
            Guid.NewGuid(), tenantId, project, Cutoff, parameters, chain, Cutoff.AddMinutes(30));

        Assert.Equal(ReportDataStatus.Available, first.DataStatus);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.SourceManifestSha256, second.SourceManifestSha256);
        Assert.Contains(reportId.ToString(), first.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("copiedFromFactId", first.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOfficialChainIsNoDataRatherThanFabricatedZero()
    {
        var tenantId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var snapshot = new DailyReportSnapshotBuilder().Build(
            Guid.NewGuid(),
            tenantId,
            Project(tenantId),
            Cutoff,
            new DailyReportReportParameters(reportId, true),
            chain: null,
            builtAt: Cutoff.AddMinutes(1));

        Assert.Equal(ReportDataStatus.NoData, snapshot.DataStatus);
        Assert.DoesNotContain("quantity\":0", snapshot.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportingRoleMappingSeparatesObserverFromRunCreation()
    {
        Assert.True(ProjectPermissionService.GrantsRole("Observer", "reporting.catalog.read"));
        Assert.True(ProjectPermissionService.GrantsRole("Observer", "reporting.output.download"));
        Assert.False(ProjectPermissionService.GrantsRole("Observer", "reporting.run.create"));
        Assert.True(ProjectPermissionService.GrantsRole("SiteSupervisor", "reporting.run.create"));
        Assert.True(ProjectPermissionService.GrantsRole("TechnicalOffice", "reporting.run.create"));
        Assert.True(ProjectPermissionService.GrantsRole("ProjectController", "field.daily-reports.read"));
    }

    [Fact]
    public void OutputManifestRejectsMediaAndExtensionSubstitution()
    {
        var invalidContentType = Assert.Throws<DomainRuleException>(() => CreateOutput(
            contentType: "application/octet-stream",
            fileName: "report.pdf"));
        var invalidExtension = Assert.Throws<DomainRuleException>(() => CreateOutput(
            contentType: "application/pdf",
            fileName: "report.xlsx"));
        var output = CreateOutput(contentType: "application/pdf", fileName: "report.pdf");

        Assert.Equal("reporting.output.content_type.invalid", invalidContentType.Code);
        Assert.Equal("reporting.output.file_name.invalid", invalidExtension.Code);
        Assert.Equal(ReportOutputArchiveState.Active, output.ArchiveState);
        output.Archive(Cutoff.AddDays(1));
        Assert.Equal(ReportOutputArchiveState.Archived, output.ArchiveState);
    }

    private static ReportRun CreateRun()
    {
        var parametersJson = CanonicalJson.Serialize(new DailyReportReportParameters(Guid.NewGuid(), true));
        return ReportRun.Queue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "daily-report-certified",
            Guid.NewGuid(),
            "1.0.0",
            parametersJson,
            CanonicalJson.Sha256(parametersJson),
            CanonicalJson.Serialize(new[] { ReportFormat.Pdf, ReportFormat.Xlsx }),
            Cutoff,
            "Asia/Tehran",
            Guid.NewGuid(),
            "{\"allowed\":true}",
            "reporting-test",
            new string('a', 64),
            Cutoff);
    }

    private static ProjectControlProfile Project(Guid tenantId) => new(
        Guid.NewGuid(),
        tenantId,
        "PRJ-001",
        "پروژه آزمون",
        "Asia/Tehran",
        "IRR",
        7,
        Cutoff.AddDays(-1),
        ProjectStatus.Active,
        ContractModel.NotConfigured,
        PlanningMode.None,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null));

    private static ReportOutput CreateOutput(string contentType, string fileName) => ReportOutput.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReportFormat.Pdf,
        contentType,
        fileName,
        Guid.NewGuid(),
        1_024,
        new string('a', 64),
        "RPT-TEST-001",
        new string('b', 64),
        ReportClassification.Internal,
        "LongTerm",
        Cutoff);

    private static DailyReportReportingChain Chain(Guid reportId)
    {
        var factId = Guid.NewGuid();
        var fact = new DailyReportReportingFact(
            factId,
            Guid.NewGuid(),
            DailyReportReportingFactKind.WorkProgress,
            "اجرای بتن‌ریزی",
            "سازه",
            Guid.NewGuid(),
            "زون A",
            12.5m,
            "m3",
            null,
            null,
            DailyReportReportingImpactLevel.Medium,
            "OBS-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Cutoff.AddHours(-3));
        var version = new DailyReportReportingVersion(
            reportId,
            reportId,
            1,
            null,
            null,
            null,
            new DateOnly(2026, 9, 18),
            "زون A",
            "گزارش رسمی",
            DailyReportReportingVersionState.Approved,
            Guid.NewGuid(),
            Cutoff.AddHours(-4),
            Guid.NewGuid(),
            Cutoff.AddHours(-2),
            Cutoff.AddHours(-2),
            null,
            null,
            4,
            [fact]);
        return new DailyReportReportingChain(reportId, reportId, Cutoff, [version]);
    }
}
