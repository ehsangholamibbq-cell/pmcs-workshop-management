using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectPeriodicReportingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly DateOnly WeeklyStart = new(2026, 9, 19);
    private static readonly DateOnly WeeklyEnd = new(2026, 9, 26);
    private static readonly DateTimeOffset ClosedCutoff =
        new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RuntimeIdentityPinsDefinitionParameterSnapshotAndSourceContracts()
    {
        Assert.Equal("PMCS-RPT1-F02-SEMANTIC-001", ProjectPeriodicReportRuntimeContract.SemanticContractId);
        Assert.Equal("project-periodic-certified", ProjectPeriodicReportRuntimeContract.DefinitionCode);
        Assert.Equal("1.0.0", ProjectPeriodicReportRuntimeContract.DefinitionVersion);
        Assert.Equal(
            "pmcs.reporting.project-periodic.parameters/v1",
            ProjectPeriodicReportRuntimeContract.ParameterSchemaVersion);
        Assert.Equal(
            "pmcs.reporting.project-periodic.snapshot/v1",
            ProjectPeriodicReportRuntimeContract.SnapshotSchemaVersion);
        Assert.Equal("1.0.0", ProjectPeriodicReportRuntimeContract.TemplateVersion);
        Assert.Equal(
            "pmcs.reporting.project-periodic.renderer/v1",
            ProjectPeriodicReportRuntimeContract.RendererContractVersion);
        Assert.Equal(
            "pmcs.reporting.project-periodic.layout/v1",
            ProjectPeriodicReportRuntimeContract.LayoutContractVersion);
        Assert.Equal(
            "pmcs.field-operations.daily-report-period/v1",
            DailyReportPeriodReportingContract.Version);
    }

    [Fact]
    public void WeeklyPeriodUsesSaturdayAndPinnedProjectTimeZoneBoundaries()
    {
        var resolved = ProjectPeriodicReportPeriodResolver.Resolve(
            new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Weekly, WeeklyStart),
            "Asia/Tehran",
            ClosedCutoff,
            ClosedCutoff.AddMinutes(1));

        Assert.Equal(WeeklyEnd, resolved.PeriodEndLocalDateExclusive);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 18, 20, 30, 0, TimeSpan.Zero),
            resolved.PeriodStartUtc);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 25, 20, 30, 0, TimeSpan.Zero),
            resolved.PeriodEndUtcExclusive);
        Assert.True(resolved.PeriodClosedAtCutoff);
        Assert.Equal(new DateOnly(2026, 9, 26), resolved.CutoffLocalDate);
    }

    [Theory]
    [InlineData("2026-08-23", "2026-09-23", 31)]
    [InlineData("2025-02-19", "2025-03-21", 30)]
    [InlineData("2026-02-20", "2026-03-21", 29)]
    public void MonthlyPeriodUsesPersianMonthBoundaries(
        string start,
        string expectedEnd,
        int expectedDays)
    {
        var startDate = DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture);
        var endDate = DateOnly.Parse(expectedEnd, System.Globalization.CultureInfo.InvariantCulture);
        var asOf = new DateTimeOffset(endDate.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        var resolved = ProjectPeriodicReportPeriodResolver.Resolve(
            new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Monthly, startDate),
            "Asia/Tehran",
            asOf,
            asOf.AddMinutes(1));

        Assert.Equal(endDate, resolved.PeriodEndLocalDateExclusive);
        Assert.Equal(expectedDays, resolved.PeriodEndLocalDateExclusive.DayNumber - startDate.DayNumber);
        Assert.True(resolved.PeriodClosedAtCutoff);
    }

    [Fact]
    public void PeriodResolverRejectsNonCanonicalStartFutureCutoffAndUnknownTimeZone()
    {
        var invalidStart = Assert.Throws<DomainRuleException>(() =>
            ProjectPeriodicReportPeriodResolver.Resolve(
                new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Weekly, WeeklyStart.AddDays(1)),
                "Asia/Tehran",
                ClosedCutoff,
                ClosedCutoff));
        var future = Assert.Throws<DomainRuleException>(() =>
            ProjectPeriodicReportPeriodResolver.Resolve(
                new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Weekly, WeeklyStart),
                "Asia/Tehran",
                ClosedCutoff.AddMinutes(1),
                ClosedCutoff));
        var beforeStart = Assert.Throws<DomainRuleException>(() =>
            ProjectPeriodicReportPeriodResolver.Resolve(
                new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Weekly, WeeklyStart),
                "Asia/Tehran",
                new DateTimeOffset(2026, 9, 18, 20, 29, 59, TimeSpan.Zero),
                ClosedCutoff));
        var unknownTimeZone = Assert.Throws<DomainRuleException>(() =>
            ProjectPeriodicReportPeriodResolver.Resolve(
                new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Weekly, WeeklyStart),
                "Iran/Unknown-City",
                ClosedCutoff,
                ClosedCutoff));

        Assert.Equal("reporting.period.start.invalid", invalidStart.Code);
        Assert.Equal("reporting.period.cutoff.future", future.Code);
        Assert.Equal("reporting.period.cutoff.before_start", beforeStart.Code);
        Assert.Equal("reporting.period.time_zone.invalid", unknownTimeZone.Code);
    }

    [Fact]
    public void ClosedDailyCadenceBuildsAvailableSnapshotWithSeparatedAggregatesAndClassification()
    {
        var roots = new[]
        {
            Root(10, WeeklyStart, Fact(110, DailyReportReportingFactKind.WorkProgress, quantity: 10m, unit: "m3")),
            Root(20, WeeklyStart.AddDays(1), Fact(120, DailyReportReportingFactKind.WorkProgress, quantity: 5m, unit: "M3")),
            Root(30, WeeklyStart.AddDays(2), Fact(130, DailyReportReportingFactKind.WorkProgress, quantity: 2m)),
            Root(40, WeeklyStart.AddDays(3), Fact(140, DailyReportReportingFactKind.Labor, resourceCount: 4, hours: 32m)),
            Root(50, WeeklyStart.AddDays(4), Fact(150, DailyReportReportingFactKind.Labor, resourceCount: 4, hours: 28m)),
            Root(
                60,
                WeeklyStart.AddDays(5),
                Fact(
                    160,
                    DailyReportReportingFactKind.Issue,
                    impactLevel: DailyReportReportingImpactLevel.High),
                DailyReportReportingClassification.Restricted),
            Root(70, WeeklyStart.AddDays(6), Fact(170, DailyReportReportingFactKind.Note))
        };

        var snapshot = BuildSnapshot(Project(), Source(ClosedCutoff, roots));
        using var payload = JsonDocument.Parse(snapshot.PayloadJson);
        var root = payload.RootElement;

        Assert.Equal(ReportDataStatus.Available, snapshot.DataStatus);
        Assert.Equal(ReportClassification.Restricted, snapshot.Classification);
        Assert.Equal(7, root.GetProperty("coverage").GetProperty("expectedSlotCount").GetInt32());
        Assert.Equal(7, root.GetProperty("coverage").GetProperty("coveredSlotCount").GetInt32());
        Assert.Empty(root.GetProperty("reasonCodes").EnumerateArray());
        Assert.Equal(3, root.GetProperty("quantityTotals").GetArrayLength());
        Assert.Contains(
            root.GetProperty("quantityTotals").EnumerateArray(),
            item => item.GetProperty("unitState").GetString() == "UnitMissing" &&
                item.GetProperty("sourceUnit").ValueKind == JsonValueKind.Null);
        Assert.Contains(
            root.GetProperty("resourceObservationTotals").EnumerateArray(),
            item => item.GetProperty("kind").GetString() == "Labor" &&
                item.GetProperty("resourceCount").GetInt32() == 8 &&
                item.GetProperty("hours").GetDecimal() == 60m);
        Assert.Single(root.GetProperty("highImpactFacts").EnumerateArray());
        Assert.DoesNotContain("progressPercentage", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grandTotal", snapshot.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkingDaysCadenceUsesOnlyPinnedCalendarDaysForCoverage()
    {
        var saturdayAndSunday = (1 << (int)DayOfWeek.Saturday) | (1 << (int)DayOfWeek.Sunday);
        var project = Project() with
        {
            ReportingFrequency = ReportingFrequency.WorkingDays,
            Calendar = new ProjectCalendarProfile(
                ProjectCalendarConfigurationState.Configured,
                saturdayAndSunday)
        };
        var roots = new[]
        {
            Root(180, WeeklyStart, Fact(181, DailyReportReportingFactKind.Note)),
            Root(190, WeeklyStart.AddDays(1), Fact(191, DailyReportReportingFactKind.Note))
        };

        var snapshot = BuildSnapshot(project, Source(ClosedCutoff, roots));
        using var payload = JsonDocument.Parse(snapshot.PayloadJson);
        var coverage = payload.RootElement.GetProperty("coverage");

        Assert.Equal(ReportDataStatus.Available, snapshot.DataStatus);
        Assert.Equal(2, coverage.GetProperty("expectedSlotCount").GetInt32());
        Assert.Equal(2, coverage.GetProperty("coveredSlotCount").GetInt32());
    }

    [Fact]
    public void WeeklyCadenceUsesOneCoveredSaturdayFridaySlot()
    {
        var project = Project() with { ReportingFrequency = ReportingFrequency.Weekly };
        var roots = new[]
        {
            Root(195, WeeklyStart.AddDays(3), Fact(196, DailyReportReportingFactKind.Note))
        };

        var snapshot = BuildSnapshot(project, Source(ClosedCutoff, roots));
        using var payload = JsonDocument.Parse(snapshot.PayloadJson);
        var coverage = payload.RootElement.GetProperty("coverage");

        Assert.Equal(ReportDataStatus.Available, snapshot.DataStatus);
        Assert.Equal(1, coverage.GetProperty("expectedSlotCount").GetInt32());
        Assert.Equal(1, coverage.GetProperty("coveredSlotCount").GetInt32());
        Assert.Equal("2026-09-25", coverage.GetProperty("expectedDates")[0].GetString());
    }

    [Fact]
    public void OpenPeriodRemainsInsufficientEvenWhenEveryElapsedDailySlotIsCovered()
    {
        var cutoff = new DateTimeOffset(2026, 9, 22, 15, 0, 0, TimeSpan.Zero);
        var roots = Enumerable.Range(0, 4)
            .Select(index => Root(
                200 + index * 10,
                WeeklyStart.AddDays(index),
                Fact(201 + index * 10, DailyReportReportingFactKind.Note),
                approvedAt: cutoff.AddHours(-1)))
            .ToArray();

        var snapshot = BuildSnapshot(Project(), Source(cutoff, roots), cutoff);
        using var payload = JsonDocument.Parse(snapshot.PayloadJson);
        var coverage = payload.RootElement.GetProperty("coverage");

        Assert.Equal(ReportDataStatus.InsufficientData, snapshot.DataStatus);
        Assert.Equal(4, coverage.GetProperty("expectedSlotCount").GetInt32());
        Assert.Equal(4, coverage.GetProperty("coveredSlotCount").GetInt32());
        var reasons = payload.RootElement.GetProperty("reasonCodes")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        Assert.Single(reasons);
        Assert.Equal("PeriodOpenAtCutoff", reasons[0]);
    }

    [Fact]
    public void MissingProjectReportingConfigurationIsExplicitlyNotConfigured()
    {
        var project = Project() with
        {
            ReportingFrequency = ReportingFrequency.NotConfigured,
            DailyReportWorkflow = DailyReportWorkflow.NotConfigured,
            DailyCutoffLocalTime = null,
            Calendar = new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null)
        };

        var snapshot = BuildSnapshot(project, Source(ClosedCutoff, []));
        using var payload = JsonDocument.Parse(snapshot.PayloadJson);
        var reasonCodes = payload.RootElement.GetProperty("reasonCodes")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Equal(ReportDataStatus.NotConfigured, snapshot.DataStatus);
        Assert.Contains("ReportingCadenceMissing", reasonCodes);
        Assert.Contains("DailyWorkflowMissing", reasonCodes);
        Assert.Contains("DailyCutoffMissing", reasonCodes);
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("coverage").GetProperty("expectedDates").ValueKind);
    }

    [Fact]
    public void ConfiguredPeriodWithoutOfficialReportsIsNoDataWithoutFabricatedMetrics()
    {
        var snapshot = BuildSnapshot(Project(), Source(ClosedCutoff, []));
        using var payload = JsonDocument.Parse(snapshot.PayloadJson);

        Assert.Equal(ReportDataStatus.NoData, snapshot.DataStatus);
        Assert.Empty(payload.RootElement.GetProperty("factCounts").EnumerateArray());
        Assert.Empty(payload.RootElement.GetProperty("quantityTotals").EnumerateArray());
        Assert.Empty(payload.RootElement.GetProperty("resourceObservationTotals").EnumerateArray());
        Assert.DoesNotContain("quantity\":0", snapshot.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingExpectedDateAndEmptyOfficialReportAreBothInsufficientReasons()
    {
        var roots = Enumerable.Range(0, 6)
            .Select(index => Root(
                300 + index * 10,
                WeeklyStart.AddDays(index),
                index == 2
                    ? Array.Empty<DailyReportReportingFact>()
                    : [Fact(301 + index * 10, DailyReportReportingFactKind.Note)]))
            .ToArray();

        var snapshot = BuildSnapshot(Project(), Source(ClosedCutoff, roots));
        using var payload = JsonDocument.Parse(snapshot.PayloadJson);
        var reasons = payload.RootElement.GetProperty("reasonCodes")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Equal(ReportDataStatus.InsufficientData, snapshot.DataStatus);
        Assert.Contains("ExpectedSlotMissing", reasons);
        Assert.Contains("OfficialReportEmpty", reasons);
        Assert.Equal(
            WeeklyStart.AddDays(6).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            payload.RootElement.GetProperty("coverage").GetProperty("missingExpectedDates")[0].GetString());
    }

    [Fact]
    public void SemanticAndManifestHashesIgnoreQueryOrderRunIdentityAndBuildTime()
    {
        var firstRoot = Root(
            400,
            WeeklyStart,
            [
                Fact(402, DailyReportReportingFactKind.Note),
                Fact(401, DailyReportReportingFactKind.WorkProgress, quantity: 3m, unit: "m3")
            ]);
        var secondRoot = Root(410, WeeklyStart.AddDays(1), Fact(411, DailyReportReportingFactKind.Note));
        var first = BuildSnapshot(Project(), Source(ClosedCutoff, [firstRoot, secondRoot]));
        var reorderedFirstRoot = firstRoot with
        {
            Versions = firstRoot.Versions
                .Select(version => version with { Facts = version.Facts.Reverse().ToArray() })
                .ToArray()
        };
        var second = BuildSnapshot(
            Project(),
            Source(ClosedCutoff, [secondRoot, reorderedFirstRoot]),
            runId: Id(999),
            builtAt: ClosedCutoff.AddDays(2));

        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.SourceManifestSha256, second.SourceManifestSha256);
    }

    [Fact]
    public void OfficialCorrectionAfterCutoffChangesSelectedLineageAndHashesOnlyAfterItsCutoff()
    {
        var reportDate = WeeklyStart;
        var v1 = Version(
            500,
            500,
            reportDate,
            1,
            [Fact(501, DailyReportReportingFactKind.Note)],
            approvedAt: new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero));
        var historicalCutoff = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero);
        var historicalRoot = new DailyReportReportingRoot(
            Id(500),
            reportDate,
            Id(500),
            DailyReportReportingClassification.Internal,
            [v1]);
        var historical = BuildSnapshot(
            Project(),
            Source(historicalCutoff, [historicalRoot]),
            historicalCutoff);

        var correctionApprovedAt = new DateTimeOffset(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
        var superseded = v1 with
        {
            State = DailyReportReportingVersionState.Superseded,
            SupersededByReportId = Id(502),
            SupersededAt = correctionApprovedAt,
            LastModifiedAt = correctionApprovedAt,
            Revision = 5
        };
        var correction = Version(
            502,
            500,
            reportDate,
            2,
            [Fact(503, DailyReportReportingFactKind.Note, copiedFromFactId: Id(501))],
            approvedAt: correctionApprovedAt,
            supersedesReportId: Id(500));
        var correctedRoot = new DailyReportReportingRoot(
            Id(500),
            reportDate,
            Id(502),
            DailyReportReportingClassification.Internal,
            [superseded, correction]);
        var corrected = BuildSnapshot(Project(), Source(ClosedCutoff, [correctedRoot]));

        Assert.NotEqual(historical.Sha256, corrected.Sha256);
        Assert.NotEqual(historical.SourceManifestSha256, corrected.SourceManifestSha256);
        Assert.DoesNotContain(Id(502).ToString(), historical.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Id(502).ToString(), corrected.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateRootDateAndDuplicateCurrentOfficialFailClosed()
    {
        var duplicateDate = Source(
            ClosedCutoff,
            [
                Root(600, WeeklyStart, Fact(601, DailyReportReportingFactKind.Note)),
                Root(610, WeeklyStart, Fact(611, DailyReportReportingFactKind.Note))
            ]);
        var duplicateRootError = Assert.Throws<DomainRuleException>(() =>
            BuildSnapshot(Project(), duplicateDate));

        var first = Version(
            620,
            620,
            WeeklyStart,
            1,
            [Fact(621, DailyReportReportingFactKind.Note)],
            approvedAt: ClosedCutoff.AddDays(-2));
        var second = Version(
            622,
            620,
            WeeklyStart,
            2,
            [Fact(623, DailyReportReportingFactKind.Note)],
            approvedAt: ClosedCutoff.AddDays(-1),
            supersedesReportId: Id(620));
        var duplicateCurrent = Source(
            ClosedCutoff,
            [new DailyReportReportingRoot(
                Id(620),
                WeeklyStart,
                Id(622),
                DailyReportReportingClassification.Internal,
                [first, second])]);
        var duplicateCurrentError = Assert.Throws<DomainRuleException>(() =>
            BuildSnapshot(Project(), duplicateCurrent));
        var futureCreatedVersion = Root(
            630,
            WeeklyStart,
            Fact(631, DailyReportReportingFactKind.Note)) with
        {
            Versions =
            [
                Version(
                    632,
                    630,
                    WeeklyStart,
                    1,
                    [Fact(633, DailyReportReportingFactKind.Note)],
                    ClosedCutoff.AddDays(-1)) with
                {
                    CreatedAt = ClosedCutoff.AddMinutes(1)
                }
            ],
            CurrentOfficialReportId = Id(632)
        };
        var futureVersionError = Assert.Throws<DomainRuleException>(() =>
            BuildSnapshot(Project(), Source(ClosedCutoff, [futureCreatedVersion])));
        var futureConfigurationError = Assert.Throws<DomainRuleException>(() =>
            BuildSnapshot(
                Project() with { ConfigurationChangedAt = ClosedCutoff.AddMinutes(2) },
                Source(ClosedCutoff, [])));

        Assert.Equal("reporting.period.source_root_date.duplicate", duplicateRootError.Code);
        Assert.Equal("reporting.period.source_current.duplicate", duplicateCurrentError.Code);
        Assert.Equal("reporting.period.source_version.invalid", futureVersionError.Code);
        Assert.Equal("reporting.period.project_scope.invalid", futureConfigurationError.Code);
    }

    private static ReportSnapshot BuildSnapshot(
        ProjectControlProfile project,
        DailyReportReportingPeriod source,
        DateTimeOffset? cutoff = null,
        Guid? runId = null,
        DateTimeOffset? builtAt = null)
    {
        var actualCutoff = cutoff ?? ClosedCutoff;
        return ProjectPeriodicReportSnapshotBuilder.Build(
            runId ?? Id(3),
            TenantId,
            project,
            actualCutoff,
            new ProjectPeriodicReportParameters(ProjectReportPeriodKind.Weekly, WeeklyStart),
            source,
            actualCutoff.AddMinutes(1),
            builtAt ?? actualCutoff.AddMinutes(2));
    }

    private static ProjectControlProfile Project() => new(
        ProjectId,
        TenantId,
        "PRJ-F02",
        "پروژه گزارش دوره‌ای",
        "Asia/Tehran",
        "IRR",
        11,
        ClosedCutoff.AddDays(-10),
        ProjectStatus.Active,
        ContractModel.GeneralContracting,
        PlanningMode.SimpleWorkList,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 127),
        4,
        new TimeOnly(18, 0),
        ReportingFrequency.Daily,
        DailyReportWorkflow.OneStepApproval);

    private static DailyReportReportingPeriod Source(
        DateTimeOffset cutoff,
        IReadOnlyCollection<DailyReportReportingRoot> roots) =>
        new(
            DailyReportPeriodReportingContract.Version,
            TenantId,
            ProjectId,
            WeeklyStart,
            WeeklyEnd,
            cutoff,
            roots);

    private static DailyReportReportingRoot Root(
        int sequence,
        DateOnly reportDate,
        DailyReportReportingFact fact,
        DailyReportReportingClassification classification = DailyReportReportingClassification.Internal,
        DateTimeOffset? approvedAt = null) =>
        Root(sequence, reportDate, [fact], classification, approvedAt);

    private static DailyReportReportingRoot Root(
        int sequence,
        DateOnly reportDate,
        IReadOnlyCollection<DailyReportReportingFact> facts,
        DailyReportReportingClassification classification = DailyReportReportingClassification.Internal,
        DateTimeOffset? approvedAt = null)
    {
        var version = Version(
            sequence + 1,
            sequence,
            reportDate,
            1,
            facts,
            approvedAt ?? ClosedCutoff.AddDays(-1));
        return new DailyReportReportingRoot(
            Id(sequence),
            reportDate,
            version.ReportId,
            classification,
            [version]);
    }

    private static DailyReportReportingVersion Version(
        int reportSequence,
        int rootSequence,
        DateOnly reportDate,
        int versionNumber,
        IReadOnlyCollection<DailyReportReportingFact> facts,
        DateTimeOffset approvedAt,
        Guid? supersedesReportId = null) =>
        new(
            Id(reportSequence),
            Id(rootSequence),
            versionNumber,
            supersedesReportId,
            null,
            null,
            reportDate,
            "زون آزمون",
            "روایت رسمی آزمون",
            DailyReportReportingVersionState.Approved,
            Id(9001),
            approvedAt.AddHours(-2),
            Id(9002),
            approvedAt,
            approvedAt,
            supersedesReportId.HasValue ? "اصلاح رسمی" : null,
            supersedesReportId.HasValue ? Id(9003) : null,
            versionNumber + 3,
            facts);

    private static DailyReportReportingFact Fact(
        int sequence,
        DailyReportReportingFactKind kind,
        decimal? quantity = null,
        string? unit = null,
        int? resourceCount = null,
        decimal? hours = null,
        DailyReportReportingImpactLevel? impactLevel = null,
        Guid? copiedFromFactId = null) =>
        new(
            Id(sequence),
            copiedFromFactId,
            kind,
            $"Fact {sequence}",
            kind.ToString(),
            Id(8001),
            "زون آزمون",
            quantity,
            unit,
            resourceCount,
            hours,
            impactLevel,
            $"REF-{sequence}",
            kind == DailyReportReportingFactKind.WorkProgress ? Id(8002) : null,
            Id(9001),
            new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero));

    private static Guid Id(int sequence) =>
        Guid.Parse($"00000000-0000-4000-8000-{sequence:000000000000}");
}
