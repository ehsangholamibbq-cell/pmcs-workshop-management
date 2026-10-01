using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.ProjectIntelligence.Contracts;
using Pmcs.Modules.ProjectIntelligence.Domain;
using Pmcs.Modules.ProjectIntelligence.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ExecutiveProjectStateReportingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 20);
    private static readonly DateTimeOffset Cutoff =
        new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly string[] NotConfiguredReasons = ["ProjectStateReportingNotConfigured"];
    private static readonly string[] MissingSnapshotReasons = ["OfficialSnapshotMissing"];
    private static readonly string[] NoDataReasons = ["OfficialSnapshotNoData"];
    private static readonly string[] InsufficientReasons =
        ["ConfidenceLow", "CoverageInsufficient", "FreshnessStale", "OfficialSnapshotInsufficient"];

    [Fact]
    public void RuntimeIdentityPinsDefinitionParameterSnapshotProfileAndSourceContracts()
    {
        Assert.Equal("PMCS-RPT1-F03-SEMANTIC-001", ExecutiveProjectStateReportRuntimeContract.SemanticContractId);
        Assert.Equal("executive-project-state-certified", ExecutiveProjectStateReportRuntimeContract.DefinitionCode);
        Assert.Equal("1.0.0", ExecutiveProjectStateReportRuntimeContract.DefinitionVersion);
        Assert.Equal(
            "pmcs.reporting.executive-project-state.parameters/v1",
            ExecutiveProjectStateReportRuntimeContract.ParameterSchemaVersion);
        Assert.Equal(
            "pmcs.reporting.executive-project-state.snapshot/v1",
            ExecutiveProjectStateReportRuntimeContract.SnapshotSchemaVersion);
        Assert.Equal(
            "pmcs.reporting.executive-project-state.project-profile/v1",
            ExecutiveProjectStateReportRuntimeContract.PinnedProjectProfileSchemaVersion);
        Assert.Equal(
            "pmcs.project-intelligence.project-state-reporting/v1",
            ProjectStateReportingContract.Version);
        Assert.Equal(14, ProjectStateReportingContract.MaximumTrendDates);

        var report = Build(Select([Snapshot(10)]));
        using var payload = JsonDocument.Parse(report.PayloadJson);
        Assert.Empty(payload.RootElement.GetProperty("parameters").EnumerateObject());
    }

    [Fact]
    public void SelectorUsesCutoffAndCanonicalAsOfCalculatedAtAndOrdinalIdentityTieBreak()
    {
        var olderDate = Snapshot(10, CutoffLocalDate.AddDays(-1), Cutoff.AddHours(-4));
        var earlierCalculation = Snapshot(20, CutoffLocalDate, Cutoff.AddHours(-3));
        var lowerIdentity = Snapshot(30, CutoffLocalDate, Cutoff.AddHours(-2));
        var selected = Snapshot(40, CutoffLocalDate, Cutoff.AddHours(-2));
        var calculatedAfterCutoff = Snapshot(50, CutoffLocalDate, Cutoff.AddMinutes(1));
        var datedAfterCutoff = Snapshot(60, CutoffLocalDate.AddDays(1), Cutoff.AddHours(-5));

        var selection = Select([
            calculatedAfterCutoff,
            lowerIdentity,
            olderDate,
            datedAfterCutoff,
            selected,
            earlierCalculation
        ]);

        Assert.Equal(selected.SnapshotId, selection.SelectedSnapshot?.SnapshotId);
        Assert.Equal(2, selection.Trend.Count);
        Assert.Equal(olderDate.SnapshotId, selection.Trend.First().SnapshotId);
        Assert.Equal(selected.SnapshotId, selection.Trend.Last().SnapshotId);
    }

    [Fact]
    public void SelectorKeepsFourteenDistinctDatesAndCollapsesSameDateRecalculations()
    {
        var snapshots = Enumerable.Range(0, 17)
            .Select(index => Snapshot(
                100 + index,
                CutoffLocalDate.AddDays(index - 16),
                Cutoff.AddDays(index - 16)))
            .Append(Snapshot(999, CutoffLocalDate.AddDays(-3), Cutoff.AddDays(-3).AddMinutes(1)))
            .Reverse()
            .ToArray();

        var selection = Select(snapshots);

        Assert.Equal(14, selection.Trend.Count);
        Assert.Equal(14, selection.Trend.Select(item => item.AsOfDate).Distinct().Count());
        Assert.Equal(CutoffLocalDate.AddDays(-13), selection.Trend.First().AsOfDate);
        Assert.Equal(CutoffLocalDate, selection.Trend.Last().AsOfDate);
        Assert.Contains(selection.Trend, item => item.SnapshotId == Id(999));
    }

    [Fact]
    public void HistoricalCutoffIgnoresLaterCorrectionAndTwinSourceOrderIsDeterministic()
    {
        var official = Snapshot(10, CutoffLocalDate, Cutoff.AddHours(-2));
        var laterCorrection = Snapshot(20, CutoffLocalDate, Cutoff.AddMinutes(10));
        var previous = Snapshot(5, CutoffLocalDate.AddDays(-1), Cutoff.AddDays(-1));

        var first = Build(Select([official, laterCorrection, previous]), runId: Id(501));
        var second = Build(Select([previous, laterCorrection, official]), runId: Id(502));

        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.SourceManifestSha256, second.SourceManifestSha256);
        Assert.Equal(first.PayloadJson, second.PayloadJson);
        Assert.Equal(first.SourceManifestJson, second.SourceManifestJson);
        Assert.DoesNotContain(laterCorrection.SnapshotId.ToString("D"), first.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void NotConfiguredSourceIsExplicitAndDoesNotFabricateOfficialState()
    {
        var selection = ProjectStateReportingSelector.Select(
            TenantId,
            ProjectId,
            CutoffLocalDate,
            Cutoff,
            ProjectStateReportingSourceState.NotConfigured,
            ProjectStateReportingClassification.Internal,
            null,
            []);

        var report = Build(selection);
        using var payload = JsonDocument.Parse(report.PayloadJson);

        Assert.Equal(ReportDataStatus.NotConfigured, report.DataStatus);
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("officialSnapshot").ValueKind);
        Assert.Equal(NotConfiguredReasons, Reasons(payload.RootElement));
    }

    [Fact]
    public void ConfiguredSourceWithoutEligibleSnapshotIsNoData()
    {
        var report = Build(Select([]));
        using var payload = JsonDocument.Parse(report.PayloadJson);

        Assert.Equal(ReportDataStatus.NoData, report.DataStatus);
        Assert.Equal(MissingSnapshotReasons, Reasons(payload.RootElement));
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("officialSnapshot").ValueKind);
        Assert.Empty(payload.RootElement.GetProperty("trend").EnumerateArray());
    }

    [Fact]
    public void OfficialNoDataSnapshotRemainsNoDataWithoutStableOrSyntheticMetrics()
    {
        var snapshot = Snapshot(10) with
        {
            OperationalStatus = ProjectOperationalStatus.NoData,
            CoverageStatus = DataCoverageStatus.NoData,
            FreshnessStatus = DataFreshnessStatus.NoData,
            ConfidenceStatus = DataConfidenceStatus.NoData,
            CoveragePercent = 0,
            ExpectedReportDays = 7,
            ApprovedReportDays = 0,
            LastApprovedReportDate = null,
            ApprovedFactCount = 0,
            ProgressFactCount = 0,
            SourceMaxChangedAt = null
        };

        var report = Build(Select([snapshot], latestApprovedSourceChangedAt: null));
        using var payload = JsonDocument.Parse(report.PayloadJson);

        Assert.Equal(ReportDataStatus.NoData, report.DataStatus);
        Assert.Equal(NoDataReasons, Reasons(payload.RootElement));
        Assert.Equal(
            "NoData",
            payload.RootElement.GetProperty("officialSnapshot").GetProperty("operationalStatus").GetString());
        Assert.DoesNotContain("healthScore", report.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InsufficientOperationalCoverageFreshnessAndConfidenceKeepIndependentReasons()
    {
        var snapshot = Snapshot(10) with
        {
            OperationalStatus = ProjectOperationalStatus.InsufficientData,
            CoverageStatus = DataCoverageStatus.Insufficient,
            FreshnessStatus = DataFreshnessStatus.Stale,
            ConfidenceStatus = DataConfidenceStatus.Low,
            CoveragePercent = 42.9m,
            ApprovedReportDays = 3
        };

        var report = Build(Select([snapshot]));
        using var payload = JsonDocument.Parse(report.PayloadJson);

        Assert.Equal(ReportDataStatus.InsufficientData, report.DataStatus);
        Assert.Equal(InsufficientReasons, Reasons(payload.RootElement));
    }

    [Fact]
    public void ProjectRevisionNewerThanSnapshotMakesItExplicitlyOutdated()
    {
        var report = Build(Select([Snapshot(10) with { ProjectConfigurationRevision = 6 }]));
        using var payload = JsonDocument.Parse(report.PayloadJson);

        Assert.Equal(ReportDataStatus.InsufficientData, report.DataStatus);
        Assert.Contains("ProjectConfigurationRevisionOutdated", Reasons(payload.RootElement));
        Assert.False(payload.RootElement.GetProperty("currency").GetProperty("projectConfigurationCurrent").GetBoolean());
    }

    [Fact]
    public void ApprovedSourceChangeAfterSnapshotMakesItExplicitlyOutdated()
    {
        var snapshot = Snapshot(10) with { SourceMaxChangedAt = Cutoff.AddHours(-3) };
        var report = Build(Select([snapshot], Cutoff.AddHours(-1)));
        using var payload = JsonDocument.Parse(report.PayloadJson);

        Assert.Equal(ReportDataStatus.InsufficientData, report.DataStatus);
        Assert.Contains("ApprovedSourceChangedAfterSnapshot", Reasons(payload.RootElement));
        Assert.False(payload.RootElement.GetProperty("currency").GetProperty("approvedSourceCurrent").GetBoolean());
    }

    [Fact]
    public void StablePartialSnapshotIsAvailableButRetainsItsBoundedAssessmentScope()
    {
        var report = Build(Select([Snapshot(10) with { IsPartial = true }]));
        using var payload = JsonDocument.Parse(report.PayloadJson);
        var official = payload.RootElement.GetProperty("officialSnapshot");

        Assert.Equal(ReportDataStatus.Available, report.DataStatus);
        Assert.Empty(Reasons(payload.RootElement));
        Assert.True(official.GetProperty("isPartial").GetBoolean());
        Assert.Equal("ApprovedDailyOperations", official.GetProperty("assessmentScope").GetString());
        Assert.Equal("Stable", official.GetProperty("operationalStatus").GetString());
    }

    [Fact]
    public void AgingFreshnessWithWatchRemainsAvailable()
    {
        var snapshot = Snapshot(10) with
        {
            OperationalStatus = ProjectOperationalStatus.Watch,
            FreshnessStatus = DataFreshnessStatus.Aging
        };

        var report = Build(Select([snapshot]));
        using var payload = JsonDocument.Parse(report.PayloadJson);

        Assert.Equal(ReportDataStatus.Available, report.DataStatus);
        Assert.Empty(Reasons(payload.RootElement));
        Assert.Contains("\"freshnessStatus\":\"Aging\"", report.PayloadJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProjectOperationalStatus.Watch)]
    [InlineData(ProjectOperationalStatus.AtRisk)]
    [InlineData(ProjectOperationalStatus.Critical)]
    public void SufficientCurrentOperationalStatesRemainAvailableWithoutCompositeHealth(
        ProjectOperationalStatus status)
    {
        var report = Build(Select([Snapshot(10) with { OperationalStatus = status }]));

        Assert.Equal(ReportDataStatus.Available, report.DataStatus);
        Assert.Contains($"\"operationalStatus\":\"{status}\"", report.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("composite", report.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("financial", report.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("earnedValue", report.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AttentionItemsUsePriorityAgeDateAndOrdinalLineageOrderingWithoutInventingImpact()
    {
        var items = new[]
        {
            Attention(41, ProjectAttentionKind.Issue, ProjectAttentionPriority.Unassessed, null, 2),
            Attention(31, ProjectAttentionKind.Issue, ProjectAttentionPriority.High, ProjectObservedImpact.High, 1),
            Attention(21, ProjectAttentionKind.Stoppage, ProjectAttentionPriority.Critical, ProjectObservedImpact.Critical, 3),
            Attention(11, ProjectAttentionKind.Issue, ProjectAttentionPriority.High, ProjectObservedImpact.High, 4)
        };
        var snapshot = Snapshot(10, attentionItems: items) with
        {
            OperationalStatus = ProjectOperationalStatus.Critical,
            IssueCount = 3,
            StoppageCount = 1,
            HighImpactCount = 2,
            CriticalImpactCount = 1,
            OldestAttentionAgeDays = 4
        };

        var report = Build(Select([snapshot]));
        using var payload = JsonDocument.Parse(report.PayloadJson);
        var attention = payload.RootElement
            .GetProperty("officialSnapshot")
            .GetProperty("attentionItems")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(
            new[] { Id(21), Id(11), Id(31), Id(41) },
            attention.Select(item => item.GetProperty("sourceFactId").GetGuid()).ToArray());
        Assert.Equal(JsonValueKind.Null, attention[^1].GetProperty("observedImpact").ValueKind);
        Assert.Equal("Unassessed", attention[^1].GetProperty("priority").GetString());
        Assert.Equal(Id(1041), attention[^1].GetProperty("sourceReportId").GetGuid());
    }

    [Fact]
    public void CrossTenantAndUnsupportedEligibleCalculationVersionFailClosed()
    {
        var crossTenant = Assert.Throws<DomainRuleException>(() => Select([
            Snapshot(10) with { TenantId = Id(999) }
        ]));
        var unsupported = Assert.Throws<DomainRuleException>(() => Select([
            Snapshot(10) with { CalculationVersion = "project-state-v999" }
        ]));

        Assert.Equal("project_state.reporting.snapshot.identity.invalid", crossTenant.Code);
        Assert.Equal("project_state.reporting.calculation_version.unsupported", unsupported.Code);
    }

    [Fact]
    public void SelectedSnapshotMustMatchTheTerminalTrendPayload()
    {
        var snapshot = Snapshot(10);
        var selection = Select([snapshot]) with
        {
            SelectedSnapshot = snapshot with { ProjectName = "Tampered Project" }
        };

        var exception = Assert.Throws<DomainRuleException>(() => Build(selection));

        Assert.Equal("reporting.executive_state.trend.invalid", exception.Code);
    }

    [Fact]
    public void AttentionSummaryMustMatchImmutableLineage()
    {
        var item = Attention(
            11,
            ProjectAttentionKind.Issue,
            ProjectAttentionPriority.Unassessed,
            null,
            1);
        var snapshot = Snapshot(10, attentionItems: [item]) with { HighImpactCount = 1 };

        var exception = Assert.Throws<DomainRuleException>(() => Build(Select([snapshot])));

        Assert.Equal("reporting.executive_state.attention.lineage_invalid", exception.Code);
    }

    [Fact]
    public void RestrictedClassificationPropagatesAndUnknownClassificationFailsClosed()
    {
        var restricted = Snapshot(10) with { Classification = ProjectStateReportingClassification.Restricted };
        var report = Build(Select(
            [restricted],
            sourceClassification: ProjectStateReportingClassification.Restricted));
        var invalid = Assert.Throws<DomainRuleException>(() => Select(
            [Snapshot(20)],
            sourceClassification: (ProjectStateReportingClassification)999));

        Assert.Equal(ReportClassification.Restricted, report.Classification);
        Assert.Equal("project_state.reporting.scope.invalid", invalid.Code);
    }

    [Fact]
    public void FutureCutoffAndUnknownTimeZoneFailClosed()
    {
        var selection = Select([Snapshot(10)]);
        var future = Assert.Throws<DomainRuleException>(() =>
            ExecutiveProjectStateReportSnapshotBuilder.Build(
                Id(500),
                TenantId,
                Profile(),
                Cutoff.AddMinutes(2),
                selection with { SourceCutoffUtc = Cutoff.AddMinutes(2) },
                Cutoff.AddMinutes(1),
                Cutoff.AddMinutes(3)));
        var unknownTimeZone = Assert.Throws<DomainRuleException>(() =>
            ExecutiveProjectStateReportSnapshotBuilder.Build(
                Id(501),
                TenantId,
                Profile() with { TimeZone = "Iran/Unknown-City" },
                Cutoff,
                selection,
                Cutoff.AddMinutes(1),
                Cutoff.AddMinutes(2)));

        Assert.Equal("reporting.executive_state.project_scope.invalid", future.Code);
        Assert.Equal("reporting.executive_state.time_zone.invalid", unknownTimeZone.Code);
    }

    [Fact]
    public void SnapshotAndManifestHashesExcludeRunWorkerAndBuildIdentity()
    {
        var selection = Select([Snapshot(10), Snapshot(9, CutoffLocalDate.AddDays(-1), Cutoff.AddDays(-1))]);
        var first = Build(selection, Id(701), Cutoff.AddMinutes(2));
        var second = Build(selection, Id(702), Cutoff.AddHours(3));

        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.SourceManifestSha256, second.SourceManifestSha256);
        Assert.NotEqual(first.RunId, second.RunId);
        Assert.NotEqual(first.BuiltAt, second.BuiltAt);
    }

    private static ReportSnapshot Build(
        ProjectStateReportingSelection selection,
        Guid? runId = null,
        DateTimeOffset? builtAt = null) =>
        ExecutiveProjectStateReportSnapshotBuilder.Build(
            runId ?? Id(500),
            TenantId,
            Profile(),
            Cutoff,
            selection,
            Cutoff.AddMinutes(1),
            builtAt ?? Cutoff.AddMinutes(2));

    private static ProjectStateReportingSelection Select(
        ProjectStateReportingSnapshot[] snapshots,
        DateTimeOffset? latestApprovedSourceChangedAt = null,
        ProjectStateReportingClassification sourceClassification = ProjectStateReportingClassification.Internal) =>
        ProjectStateReportingSelector.Select(
            TenantId,
            ProjectId,
            CutoffLocalDate,
            Cutoff,
            ProjectStateReportingSourceState.Configured,
            sourceClassification,
            latestApprovedSourceChangedAt ?? (snapshots.Length == 0 ? null : Cutoff.AddHours(-2)),
            snapshots);

    private static ProjectStateReportingSnapshot Snapshot(
        int id,
        DateOnly? asOfDate = null,
        DateTimeOffset? calculatedAt = null,
        IReadOnlyCollection<ProjectStateReportingAttentionItem>? attentionItems = null)
    {
        var date = asOfDate ?? CutoffLocalDate;
        var calculated = calculatedAt ?? Cutoff.AddHours(-1);
        var attention = attentionItems ?? [];
        return new ProjectStateReportingSnapshot(
            Id(id),
            TenantId,
            ProjectId,
            "PRJ-01",
            "Project One",
            ProjectStateReportingClassification.Internal,
            ProjectStateCalculator.CalculationVersion,
            7,
            date,
            calculated,
            date.AddDays(-6),
            date,
            ProjectAssessmentScope.ApprovedDailyOperations,
            IsPartial: true,
            ProjectOperationalStatus.Stable,
            DataCoverageStatus.Sufficient,
            DataFreshnessStatus.Current,
            DataConfidenceStatus.Adequate,
            ProjectCoverageBasis.ConfiguredWorkingDays,
            100m,
            7,
            7,
            date,
            12,
            3,
            2,
            2,
            1,
            attention.Count(item => item.Kind == ProjectAttentionKind.Issue),
            attention.Count(item => item.Kind == ProjectAttentionKind.Stoppage),
            attention.Count(item => item.Priority == ProjectAttentionPriority.High),
            attention.Count(item => item.Priority == ProjectAttentionPriority.Critical),
            attention.Count == 0 ? null : attention.Max(item => item.AgeDays),
            ProjectFeatureState.Active,
            ProjectFeatureState.Active,
            ProjectFeatureState.SetupRequired,
            ProjectFeatureState.NotEnabled,
            ProjectFeatureState.NotConfigured,
            calculated.AddMinutes(-1),
            attention);
    }

    private static ProjectStateReportingAttentionItem Attention(
        int id,
        ProjectAttentionKind kind,
        ProjectAttentionPriority priority,
        ProjectObservedImpact? impact,
        int ageDays) => new(
        Id(1000 + id),
        Id(id),
        CutoffLocalDate.AddDays(-ageDays),
        kind,
        $"Attention {id}",
        null,
        null,
        "Level 1",
        impact,
        priority,
        ageDays,
        ageDays switch
        {
            <= 1 => ProjectAttentionAgeBand.New,
            <= 6 => ProjectAttentionAgeBand.Aging,
            _ => ProjectAttentionAgeBand.Overdue
        },
        ProjectAttentionStatus.NeedsTriage,
        null);

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-01",
        "Project One",
        "Asia/Tehran",
        "IRR",
        7,
        Cutoff.AddDays(-1),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.None,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.SetupRequired,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 127),
        ConfigurationVersion: 3);

    private static string[] Reasons(JsonElement root) =>
        root.GetProperty("reasonCodes")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}
