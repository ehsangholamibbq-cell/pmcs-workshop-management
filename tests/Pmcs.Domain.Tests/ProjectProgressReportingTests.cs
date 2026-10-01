using System.Text.Json;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Services;
using Pmcs.Modules.Planning.Contracts;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Planning.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ProjectProgressReportingTests
{
    private static readonly Guid TenantId = Id(1);
    private static readonly Guid ProjectId = Id(2);
    private static readonly Guid BaselineId = Id(10);
    private static readonly Guid EntryId = Id(20);
    private static readonly Guid MeasurementId = Id(30);
    private static readonly DateOnly CutoffLocalDate = new(2026, 9, 20);
    private static readonly DateTimeOffset Cutoff =
        new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RuntimeIdentityPinsEmptyParametersSnapshotProfileAndBothSourceContracts()
    {
        Assert.Equal("PMCS-RPT1-F04-SEMANTIC-001", ProjectProgressReportRuntimeContract.SemanticContractId);
        Assert.Equal("project-progress-certified", ProjectProgressReportRuntimeContract.DefinitionCode);
        Assert.Equal("1.0.0", ProjectProgressReportRuntimeContract.DefinitionVersion);
        Assert.Equal(
            "pmcs.reporting.project-progress.parameters/v1",
            ProjectProgressReportRuntimeContract.ParameterSchemaVersion);
        Assert.Equal(
            "pmcs.reporting.project-progress.snapshot/v1",
            ProjectProgressReportRuntimeContract.SnapshotSchemaVersion);
        Assert.Equal(
            "pmcs.planning.project-progress-reporting/v1",
            ProjectProgressReportingContract.Version);
        Assert.Equal(
            "pmcs.field-operations.progress-evidence-reporting/v1",
            ProgressEvidenceReportingContract.Version);

        var report = Build(Calculate());
        using var payload = JsonDocument.Parse(report.PayloadJson);
        Assert.Empty(payload.RootElement.GetProperty("parameters").EnumerateObject());
        Assert.DoesNotContain("forecast", report.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("earnedValue", report.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectorUsesApprovalAndSupersessionLifecycleAtCutoffWithoutSplicingBaselines()
    {
        var oldBaseline = Baseline(
            BaselineId,
            PlanningBaselineKind.MeasurementWeights,
            [QuantityEntry(EntryId, MeasurementId, 100m, approvedAt: Cutoff.AddDays(-20))],
            Cutoff.AddDays(-20),
            Cutoff.AddHours(1));
        var futureBaseline = Baseline(
            Id(11),
            PlanningBaselineKind.MeasurementWeights,
            [QuantityEntry(Id(21), Id(31), 100m, approvedAt: Cutoff.AddHours(1))],
            Cutoff.AddHours(1));

        var selection = Select(Projection(
            PlanningMode.SimpleWorkList,
            [futureBaseline, oldBaseline],
            Evidence()));

        Assert.Equal(oldBaseline.BaselineId, selection.Baseline?.BaselineId);
        Assert.Single(selection.BaselineLineage);
        Assert.DoesNotContain(futureBaseline.BaselineId, selection.BaselineLineage.Select(item => item.BaselineId));
    }

    [Fact]
    public void OverlappingEffectiveBaselinesFailClosedWithoutLatestTieBreak()
    {
        var exception = Assert.Throws<DomainRuleException>(() => Select(Projection(
            PlanningMode.SimpleWorkList,
            [
                MeasurementBaseline(BaselineId, EntryId, MeasurementId, Cutoff.AddDays(-10)),
                MeasurementBaseline(Id(11), Id(21), Id(31), Cutoff.AddDays(-5))
            ],
            Evidence())));

        Assert.Equal("planning.progress_reporting.baseline.overlap", exception.Code);
    }

    [Fact]
    public void DailyReportCorrectionOfficialAfterCutoffDoesNotRewriteHistoricalEvidence()
    {
        var reportDate = CutoffLocalDate.AddDays(-1);
        var old = EvidenceVersion(
            Id(100), Id(99), 1, reportDate, Cutoff.AddDays(-3), Cutoff.AddHours(1),
            [Fact(Id(101), MeasurementId, 25m, "m3", Cutoff.AddDays(-3))]);
        var correction = EvidenceVersion(
            Id(102), Id(99), 2, reportDate, Cutoff.AddHours(1), null,
            [Fact(Id(103), MeasurementId, 90m, "m3", Cutoff.AddHours(1))]);

        var evidence = ProgressEvidenceReportingSelector.Select(
            TenantId,
            ProjectId,
            CutoffLocalDate,
            Cutoff,
            [correction, old]);
        var result = Calculate(evidence: evidence);

        Assert.Equal(old.ReportId, evidence.Roots.Single().CurrentOfficialReportId);
        Assert.Equal(25m, result.Summary?.ActualPercent);
        Assert.DoesNotContain(
            correction.ReportId.ToString("D"),
            JsonSerializer.Serialize(result.SourceManifest),
            StringComparison.Ordinal);
    }

    [Fact]
    public void MilestoneSelectionUsesStatusDateApprovalAndOrdinalUpdateIdentity()
    {
        var baseline = MilestoneBaseline();
        var updates = new[]
        {
            Milestone(Id(300), CutoffLocalDate.AddDays(-2), 20m, Cutoff.AddDays(-3)),
            Milestone(Id(301), CutoffLocalDate.AddDays(-1), 40m, Cutoff.AddDays(-2)),
            Milestone(Id(302), CutoffLocalDate.AddDays(-1), 55m, Cutoff.AddDays(-1)),
            Milestone(Id(303), CutoffLocalDate.AddDays(-1), 65m, Cutoff.AddDays(-1))
        };

        var result = Calculate(
            PlanningMode.Milestones,
            baseline,
            Evidence(),
            updates);

        Assert.Equal(65m, result.Summary?.ActualPercent);
        Assert.Equal(65m, result.Milestones.Single().ApprovedProgressPercent);
        Assert.Equal(CutoffLocalDate.AddDays(-1), result.Milestones.Single().LatestApprovedStatusDate);
    }

    [Fact]
    public void PlanningModeNoneIsNotConfiguredWithoutSyntheticPercentOrCurve()
    {
        var result = Calculate(Projection(PlanningMode.None, [], Evidence()));

        Assert.Equal(ProjectProgressReportingDataStatus.NotConfigured, result.DataStatus);
        Assert.Equal(new[] { ProjectProgressReportingReasonCode.PlanningModeNone }, result.ReasonCodes);
        Assert.Null(result.Summary);
        Assert.Empty(result.Curve);
    }

    [Fact]
    public void ConfiguredPlanningWithoutOfficialBaselineIsNoData()
    {
        var result = Calculate(Projection(PlanningMode.WbsBaseline, [], Evidence()));

        Assert.Equal(ProjectProgressReportingDataStatus.NoData, result.DataStatus);
        Assert.Equal(new[] { ProjectProgressReportingReasonCode.OfficialBaselineMissing }, result.ReasonCodes);
        Assert.Null(result.Baseline);
    }

    [Fact]
    public void BaselineKindMismatchIsInsufficientAndDoesNotCalculateMetrics()
    {
        var result = Calculate(
            PlanningMode.WbsBaseline,
            MeasurementBaseline(),
            Evidence(Fact(Id(100), MeasurementId, 20m)));

        Assert.Equal(ProjectProgressReportingDataStatus.InsufficientData, result.DataStatus);
        Assert.Equal(
            new[] { ProjectProgressReportingReasonCode.PlanningModeBaselineMismatch },
            result.ReasonCodes);
        Assert.Null(result.Summary);
        Assert.All(result.Entries, entry => Assert.Null(entry.ActualPercent));
    }

    [Fact]
    public void MissingOfficialActualKeepsPlannedAndNeverConvertsNullToZero()
    {
        var result = Calculate(
            PlanningMode.WbsBaseline,
            ScheduledBaseline(),
            Evidence());

        Assert.Equal(ProjectProgressReportingDataStatus.InsufficientData, result.DataStatus);
        Assert.Equal(ProjectProgressMetricStatus.NoData, result.ActualStatus);
        Assert.Contains(ProjectProgressReportingReasonCode.OfficialActualMissing, result.ReasonCodes);
        Assert.Null(result.Summary?.ActualPercent);
        Assert.NotNull(result.Summary?.PlannedPercent);
        Assert.Null(result.Summary?.VariancePercent);
    }

    [Fact]
    public void OneMissingWeightedEntryMakesAggregateActualNullWithExactMissingCount()
    {
        var baseline = Baseline(
            BaselineId,
            PlanningBaselineKind.MeasurementWeights,
            [
                QuantityEntry(EntryId, MeasurementId, 60m),
                QuantityEntry(Id(21), Id(31), 40m)
            ]);
        var result = Calculate(
            PlanningMode.SimpleWorkList,
            baseline,
            Evidence(Fact(Id(100), MeasurementId, 50m)));

        Assert.Equal(ProjectProgressReportingDataStatus.InsufficientData, result.DataStatus);
        Assert.Equal(1, result.Summary?.MissingActualEntryCount);
        Assert.Null(result.Summary?.ActualPercent);
        Assert.Contains(ProjectProgressReportingReasonCode.OfficialActualIncomplete, result.ReasonCodes);
    }

    [Fact]
    public void QuantityOverrunIsPreservedOnRowButCappedOnlyInWeightedAggregate()
    {
        var result = Calculate(evidence: Evidence(Fact(Id(100), MeasurementId, 150m)));

        Assert.Equal(150m, result.Entries.Single().ApprovedQuantity);
        Assert.Equal(150m, result.Entries.Single().ActualPercent);
        Assert.Equal(100m, result.Summary?.ActualPercent);
    }

    [Fact]
    public void MeasurementWeightsCanBeAvailableWhileScheduleAndCurveRemainNotConfigured()
    {
        var result = Calculate(evidence: Evidence(Fact(Id(100), MeasurementId, 70m)));

        Assert.Equal(ProjectProgressReportingDataStatus.Available, result.DataStatus);
        Assert.Equal(ProjectProgressMetricStatus.Available, result.ActualStatus);
        Assert.Equal(ProjectProgressMetricStatus.NotConfigured, result.ScheduleStatus);
        Assert.Equal(ProjectProgressMetricStatus.NotConfigured, result.CurveStatus);
        Assert.Null(result.Summary?.PlannedPercent);
        Assert.Empty(result.Curve);
        Assert.Contains(
            ProjectProgressReportingReasonCode.ScheduleNotConfiguredForMeasurementWeights,
            result.ReasonCodes);
    }

    [Fact]
    public void WorkingCalendarCountsOnlyPinnedWorkingDaysForLinearActivity()
    {
        var result = Calculate(
            PlanningMode.WbsBaseline,
            ScheduledBaseline(),
            Evidence(Fact(Id(100), MeasurementId, 80m)),
            calendarState: ProjectProgressCalendarState.WorkingWeek,
            workingDaysMask: 62);

        Assert.Equal(ProjectProgressCalendarBasis.WorkingDays, result.CalendarBasis);
        Assert.Equal(83.33m, result.Summary?.PlannedPercent);
        Assert.DoesNotContain(ProjectProgressReportingReasonCode.CalendarDaysFallback, result.ReasonCodes);
    }

    [Fact]
    public void MissingCalendarUsesExplicitCalendarDaysFallback()
    {
        var result = Calculate(
            PlanningMode.WbsBaseline,
            ScheduledBaseline(),
            Evidence(Fact(Id(100), MeasurementId, 80m)));

        Assert.Equal(ProjectProgressCalendarBasis.CalendarDays, result.CalendarBasis);
        Assert.Equal(87.50m, result.Summary?.PlannedPercent);
        Assert.Contains(ProjectProgressReportingReasonCode.CalendarDaysFallback, result.ReasonCodes);
    }

    [Fact]
    public void VarianceIsAlwaysActualMinusPlannedWithItsOriginalSign()
    {
        var result = Calculate(
            PlanningMode.WbsBaseline,
            ScheduledBaseline(),
            Evidence(Fact(Id(100), MeasurementId, 70m)));

        Assert.Equal(70m, result.Summary?.ActualPercent);
        Assert.Equal(87.50m, result.Summary?.PlannedPercent);
        Assert.Equal(-17.50m, result.Summary?.VariancePercent);
    }

    [Fact]
    public void ShortCurveContainsEveryInclusiveDateAndTheCutoff()
    {
        var baseline = ScheduledBaseline(
            CutoffLocalDate.AddDays(-2),
            CutoffLocalDate.AddDays(2));
        var result = Calculate(
            PlanningMode.WbsBaseline,
            baseline,
            Evidence(Fact(Id(100), MeasurementId, 50m)));

        Assert.Equal(ProjectProgressCurveSamplingKind.DailyInclusiveV1, result.Sampling.Kind);
        Assert.False(result.Sampling.IsSampled);
        Assert.Equal(5, result.Curve.Count);
        Assert.Contains(result.Curve, point => point.PointDate == CutoffLocalDate);
    }

    [Fact]
    public void LongCurveUsesUniform365GridPlusCutoffAndNeverExceeds366Points()
    {
        var baseline = ScheduledBaseline(
            CutoffLocalDate.AddDays(-1_000),
            CutoffLocalDate.AddDays(1_002));
        var result = Calculate(
            PlanningMode.WbsBaseline,
            baseline,
            Evidence(Fact(Id(100), MeasurementId, 50m)));

        Assert.Equal(ProjectProgressCurveSamplingKind.Uniform365PlusCutoffV1, result.Sampling.Kind);
        Assert.True(result.Sampling.IsSampled);
        Assert.InRange(result.Curve.Count, 365, 366);
        Assert.Contains(result.Curve, point => point.PointDate == CutoffLocalDate);
    }

    [Fact]
    public void FutureCurvePointsKeepPlannedButHaveNullActualAndVariance()
    {
        var result = Calculate(
            PlanningMode.WbsBaseline,
            ScheduledBaseline(CutoffLocalDate.AddDays(-1), CutoffLocalDate.AddDays(2)),
            Evidence(Fact(Id(100), MeasurementId, 50m)));
        var future = result.Curve.Where(point => point.PointDate > CutoffLocalDate).ToArray();

        Assert.NotEmpty(future);
        Assert.All(future, point =>
        {
            Assert.NotNull(point.PlannedPercent);
            Assert.Null(point.ActualPercent);
            Assert.Null(point.VariancePercent);
        });
    }

    [Fact]
    public void ApprovedProgressOutsideBaselineIsCountedWithoutChangingAggregate()
    {
        var result = Calculate(evidence: Evidence(
            Fact(Id(100), MeasurementId, 40m),
            Fact(Id(101), Id(999), 900m)));

        Assert.Equal(1, result.ApprovedProgressOutsideBaselineCount);
        Assert.Equal(40m, result.Summary?.ActualPercent);
        Assert.Contains(ProjectProgressReportingReasonCode.ApprovedProgressOutsideBaseline, result.ReasonCodes);
    }

    [Fact]
    public void TwinRunsWithDifferentQueryOrderRunIdAndBuildTimeAreDeterministic()
    {
        var firstEvidence = Evidence(
            Fact(Id(101), Id(999), 5m),
            Fact(Id(100), MeasurementId, 40m));
        var secondEvidence = Evidence(
            Fact(Id(100), MeasurementId, 40m),
            Fact(Id(101), Id(999), 5m));
        var first = Build(Calculate(evidence: firstEvidence), Id(500), Cutoff.AddMinutes(2));
        var second = Build(Calculate(evidence: secondEvidence), Id(501), Cutoff.AddHours(2));

        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.SourceManifestSha256, second.SourceManifestSha256);
        Assert.Equal(first.PayloadJson, second.PayloadJson);
        Assert.Equal(first.SourceManifestJson, second.SourceManifestJson);
    }

    [Fact]
    public void CrossTenantEvidenceAndUnknownClassificationFailClosed()
    {
        var crossTenant = Evidence() with { TenantId = Id(999) };
        var scope = Assert.Throws<DomainRuleException>(() => Select(Projection(
            PlanningMode.SimpleWorkList,
            [MeasurementBaseline()],
            crossTenant)));
        var invalidClassification = Assert.Throws<DomainRuleException>(() => Select(Projection(
            PlanningMode.SimpleWorkList,
            [MeasurementBaseline()],
            Evidence(),
            classification: (ProjectProgressReportingClassification)999)));

        Assert.Equal("planning.progress_reporting.evidence.scope.invalid", scope.Code);
        Assert.Equal("planning.progress_reporting.configuration.invalid", invalidClassification.Code);
    }

    [Fact]
    public void RestrictedSourceClassificationPropagatesWithoutDowngrade()
    {
        var result = Calculate(Projection(
            PlanningMode.SimpleWorkList,
            [MeasurementBaseline() with
            {
                Classification = ProjectProgressReportingClassification.Restricted
            }],
            Evidence(Fact(Id(100), MeasurementId, 20m))));
        var report = Build(result);

        Assert.Equal(ProjectProgressReportingClassification.Restricted, result.Classification);
        Assert.Equal(ReportClassification.Restricted, report.Classification);
    }

    [Fact]
    public void UnitMismatchAgainstPinnedTargetFailsClosedWithoutConversion()
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            Calculate(evidence: Evidence(Fact(Id(100), MeasurementId, 20m, "kg"))));

        Assert.Equal("planning.progress_reporting.evidence.unit_mismatch", exception.Code);
    }

    [Fact]
    public void InvalidTargetSnapshotAndWeightsFailClosed()
    {
        var approvedAt = Cutoff.AddDays(-10);
        var invalidTarget = QuantityEntry(EntryId, MeasurementId, 100m, approvedAt: approvedAt) with
        {
            PinnedTarget = new ProjectProgressPinnedMeasurementTarget(
                MeasurementId, "M-01", "Concrete", "m3", 100m, 1, approvedAt.AddSeconds(-1))
        };
        var target = Assert.Throws<DomainRuleException>(() => Select(Projection(
            PlanningMode.SimpleWorkList,
            [Baseline(BaselineId, PlanningBaselineKind.MeasurementWeights, [invalidTarget], approvedAt)],
            Evidence())));
        var weights = Assert.Throws<DomainRuleException>(() => Select(Projection(
            PlanningMode.SimpleWorkList,
            [Baseline(
                BaselineId,
                PlanningBaselineKind.MeasurementWeights,
                [QuantityEntry(EntryId, MeasurementId, 99m)],
                approvedAt)],
            Evidence())));

        Assert.Equal("planning.progress_reporting.baseline.target_snapshot.invalid", target.Code);
        Assert.Equal("planning.progress_reporting.baseline.weights.invalid", weights.Code);
    }

    [Fact]
    public void FutureCutoffAndUnknownTimeZoneFailClosedAtSnapshotBoundary()
    {
        var source = Calculate(evidence: Evidence(Fact(Id(100), MeasurementId, 20m)));
        var future = Assert.Throws<DomainRuleException>(() => ProjectProgressReportSnapshotBuilder.Build(
            Id(500),
            TenantId,
            Profile(),
            Cutoff.AddMinutes(2),
            source with { SourceCutoffUtc = Cutoff.AddMinutes(2) },
            Cutoff.AddMinutes(1),
            Cutoff.AddMinutes(3)));
        var timeZone = Assert.Throws<DomainRuleException>(() => ProjectProgressReportSnapshotBuilder.Build(
            Id(501),
            TenantId,
            Profile() with { TimeZone = "Iran/Unknown-City" },
            Cutoff,
            source,
            Cutoff.AddMinutes(1),
            Cutoff.AddMinutes(2)));

        Assert.Equal("reporting.project_progress.project_scope.invalid", future.Code);
        Assert.Equal("reporting.project_progress.time_zone.invalid", timeZone.Code);
    }

    private static ProjectProgressReportingResult Calculate(
        PlanningMode mode = PlanningMode.SimpleWorkList,
        ProjectProgressBaselineVersion? baseline = null,
        ProgressEvidenceReportingProjection? evidence = null,
        IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion>? milestones = null,
        ProjectProgressCalendarState calendarState = ProjectProgressCalendarState.NotConfigured,
        int? workingDaysMask = null) => ProjectProgressReportingCalculator.Calculate(Select(Projection(
        mode,
        [baseline ?? MeasurementBaseline()],
        evidence ?? Evidence(Fact(Id(100), MeasurementId, 50m)),
        milestones,
        calendarState,
        workingDaysMask)));

    private static ProjectProgressReportingResult Calculate(ProjectProgressReportingProjection projection) =>
        ProjectProgressReportingCalculator.Calculate(Select(projection));

    private static ProjectProgressReportingSelection Select(ProjectProgressReportingProjection projection) =>
        ProjectProgressReportingSelector.Select(projection);

    private static ProjectProgressReportingProjection Projection(
        PlanningMode mode,
        IReadOnlyCollection<ProjectProgressBaselineVersion> baselines,
        ProgressEvidenceReportingProjection evidence,
        IReadOnlyCollection<ProjectProgressMilestoneUpdateVersion>? milestones = null,
        ProjectProgressCalendarState calendarState = ProjectProgressCalendarState.NotConfigured,
        int? workingDaysMask = null,
        ProjectProgressReportingClassification classification = ProjectProgressReportingClassification.Internal) => new(
        ProjectProgressReportingContract.Version,
        TenantId,
        ProjectId,
        CutoffLocalDate,
        Cutoff,
        [new ProjectProgressConfigurationVersion(
            7,
            12,
            ProgressReportingEnabled: true,
            mode,
            calendarState,
            workingDaysMask,
            7,
            Cutoff.AddDays(-100),
            null,
            classification)],
        baselines,
        milestones ?? [],
        evidence);

    private static ProjectProgressBaselineVersion MeasurementBaseline(
        Guid? baselineId = null,
        Guid? entryId = null,
        Guid? measurementId = null,
        DateTimeOffset? approvedAt = null) => Baseline(
        baselineId ?? BaselineId,
        PlanningBaselineKind.MeasurementWeights,
        [QuantityEntry(
            entryId ?? EntryId,
            measurementId ?? MeasurementId,
            100m,
            approvedAt: approvedAt ?? Cutoff.AddDays(-10))],
        approvedAt ?? Cutoff.AddDays(-10));

    private static ProjectProgressBaselineVersion ScheduledBaseline(
        DateOnly? start = null,
        DateOnly? finish = null)
    {
        var approvedAt = Cutoff.AddDays(-20);
        return Baseline(
            BaselineId,
            PlanningBaselineKind.WbsBaseline,
            [QuantityEntry(
                EntryId,
                MeasurementId,
                100m,
                start ?? CutoffLocalDate.AddDays(-6),
                finish ?? CutoffLocalDate.AddDays(1),
                PlanningEntryKind.Activity,
                approvedAt)],
            approvedAt);
    }

    private static ProjectProgressBaselineVersion MilestoneBaseline()
    {
        var approvedAt = Cutoff.AddDays(-20);
        return Baseline(
            BaselineId,
            PlanningBaselineKind.MilestonePlan,
            [new ProjectProgressBaselineEntry(
                EntryId,
                null,
                "MS-01",
                "Handover",
                PlanningEntryKind.Milestone,
                ProgressMeasurementMethod.ManualPercent,
                null,
                CutoffLocalDate.AddDays(2),
                CutoffLocalDate.AddDays(2),
                100m,
                1,
                null)],
            approvedAt);
    }

    private static ProjectProgressBaselineVersion Baseline(
        Guid id,
        PlanningBaselineKind kind,
        IReadOnlyCollection<ProjectProgressBaselineEntry> entries,
        DateTimeOffset? approvedAt = null,
        DateTimeOffset? supersededAt = null)
    {
        var approval = approvedAt ?? Cutoff.AddDays(-10);
        return new ProjectProgressBaselineVersion(
            id,
            $"BL-{id.ToString("N")[^4..]}",
            "Official Baseline",
            kind,
            3,
            approval,
            supersededAt,
            null,
            null,
            ProjectProgressReportingClassification.Internal,
            entries);
    }

    private static ProjectProgressBaselineEntry QuantityEntry(
        Guid entryId,
        Guid measurementId,
        decimal weight,
        DateOnly? plannedStart = null,
        DateOnly? plannedFinish = null,
        PlanningEntryKind kind = PlanningEntryKind.MeasurementItem,
        DateTimeOffset? approvedAt = null)
    {
        var approval = approvedAt ?? Cutoff.AddDays(-10);
        return new ProjectProgressBaselineEntry(
            entryId,
            null,
            $"E-{entryId.ToString("N")[^4..]}",
            "Concrete",
            kind,
            ProgressMeasurementMethod.QuantityBased,
            measurementId,
            plannedStart,
            plannedFinish,
            weight,
            1,
            new ProjectProgressPinnedMeasurementTarget(
                measurementId,
                $"M-{measurementId.ToString("N")[^4..]}",
                "Concrete",
                "m3",
                100m,
                1,
                approval));
    }

    private static ProjectProgressMilestoneUpdateVersion Milestone(
        Guid id,
        DateOnly statusDate,
        decimal percent,
        DateTimeOffset approvedAt) => new(
        id,
        BaselineId,
        EntryId,
        statusDate,
        percent,
        2,
        approvedAt,
        null,
        ProjectProgressReportingClassification.Internal);

    private static ProgressEvidenceReportingProjection Evidence(
        params ProgressEvidenceReportingFact[] facts)
    {
        if (facts.Length == 0)
        {
            return ProgressEvidenceReportingSelector.Select(
                TenantId,
                ProjectId,
                CutoffLocalDate,
                Cutoff,
                []);
        }

        return ProgressEvidenceReportingSelector.Select(
            TenantId,
            ProjectId,
            CutoffLocalDate,
            Cutoff,
            [EvidenceVersion(
                Id(90),
                Id(90),
                1,
                CutoffLocalDate.AddDays(-1),
                Cutoff.AddDays(-2),
                null,
                facts)]);
    }

    private static ProgressEvidenceReportingVersion EvidenceVersion(
        Guid reportId,
        Guid rootId,
        int version,
        DateOnly reportDate,
        DateTimeOffset approvedAt,
        DateTimeOffset? supersededAt,
        IReadOnlyCollection<ProgressEvidenceReportingFact> facts) => new(
        reportId,
        rootId,
        version,
        reportDate,
        approvedAt,
        supersededAt,
        facts);

    private static ProgressEvidenceReportingFact Fact(
        Guid id,
        Guid? measurementId,
        decimal? quantity,
        string? unit = "m3",
        DateTimeOffset? createdAt = null) => new(
        id,
        measurementId,
        quantity,
        unit,
        createdAt ?? Cutoff.AddDays(-3));

    private static ReportSnapshot Build(
        ProjectProgressReportingResult source,
        Guid? runId = null,
        DateTimeOffset? builtAt = null) => ProjectProgressReportSnapshotBuilder.Build(
        runId ?? Id(500),
        TenantId,
        Profile(),
        Cutoff,
        source,
        Cutoff.AddMinutes(1),
        builtAt ?? Cutoff.AddMinutes(2));

    private static ProjectControlProfile Profile() => new(
        ProjectId,
        TenantId,
        "PRJ-01",
        "Project One",
        "Asia/Tehran",
        "IRR",
        12,
        Cutoff.AddDays(-1),
        ProjectStatus.Active,
        ContractModel.ConstructionManagement,
        PlanningMode.SimpleWorkList,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        ProjectFeatureState.SetupRequired,
        ProjectFeatureState.NotEnabled,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.Active,
        ProjectFeatureState.Active,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.Configured, 62),
        ConfigurationVersion: 7);

    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
}
