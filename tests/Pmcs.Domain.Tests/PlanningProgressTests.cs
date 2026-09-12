using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Planning.Domain;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Domain.Tests;

public sealed class PlanningProgressTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlanningNoneNeverFabricatesOverallOrScheduleMetrics()
    {
        var itemId = Guid.NewGuid();
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.None,
            [new MeasurementItemInput(itemId, "CON-01", "Concrete", "m3", 100m, true)],
            [
                new ProgressObservationInput(
                    Guid.NewGuid(), itemId, new DateOnly(2026, 9, 10), ProgressReviewState.Approved, 25m),
                new ProgressObservationInput(
                    Guid.NewGuid(), itemId, new DateOnly(2026, 9, 11), ProgressReviewState.Provisional, 10m)
            ]);

        Assert.Null(calculation.OfficialOverallPhysicalPercent);
        Assert.Null(calculation.ScheduleVariancePercent);
        Assert.Null(calculation.ForecastCompletionDate);
        var row = Assert.Single(calculation.Items);
        Assert.Equal(25m, row.ApprovedQuantity);
        Assert.Equal(10m, row.ProvisionalQuantity);
        Assert.Equal(25m, row.ApprovedCompletionPercent);
    }

    [Fact]
    public void UnlinkedProgressIsPreservedInsteadOfForcedIntoMeasurementCatalog()
    {
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.SimpleWorkList,
            [],
            [
                new ProgressObservationInput(
                    Guid.NewGuid(), null, new DateOnly(2026, 9, 11), ProgressReviewState.Approved, 7m),
                new ProgressObservationInput(
                    Guid.NewGuid(), null, new DateOnly(2026, 9, 11), ProgressReviewState.Provisional, null)
            ]);

        Assert.Equal(1, calculation.UnlinkedApprovedFactCount);
        Assert.Equal(1, calculation.UnlinkedProvisionalFactCount);
        Assert.Equal(MeasurementBasisState.NotConfigured, calculation.MeasurementBasisState);
    }

    [Fact]
    public void TargetDoesNotTurnMissingApprovedMeasurementIntoZeroProgress()
    {
        var itemId = Guid.NewGuid();
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.SimpleWorkList,
            [new MeasurementItemInput(itemId, "CON-01", "Concrete", "m3", 100m, true)],
            [new ProgressObservationInput(
                Guid.NewGuid(), itemId, new DateOnly(2026, 9, 11), ProgressReviewState.Provisional, 12m)]);

        var row = Assert.Single(calculation.Items);
        Assert.Null(row.ApprovedQuantity);
        Assert.Null(row.ApprovedCompletionPercent);
        Assert.Equal(12m, row.ProvisionalQuantity);
    }

    [Fact]
    public void MeasurementItemTargetIsOptionalAndUnitIsImmutableThroughAmendment()
    {
        var item = MeasurementItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EXC-01", "Excavation", "m3",
            null, null, Guid.NewGuid(), Now);

        item.Amend(1, "Foundation excavation", 450m, "Measured by survey", Guid.NewGuid(), Now.AddMinutes(1));

        Assert.Equal("m3", item.Unit);
        Assert.Equal(450m, item.TargetQuantity);
        Assert.Equal(2, item.Revision);
    }

    [Fact]
    public void MeasurementItemRejectsNonPositiveTarget()
    {
        var exception = Assert.Throws<DomainRuleException>(() => MeasurementItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EXC-01", "Excavation", "m3",
            0m, null, Guid.NewGuid(), Now));

        Assert.Equal("measurement_item.target.invalid", exception.Code);
    }

    [Fact]
    public void ApprovedMeasurementWeightsProduceOfficialOverallWithoutScheduleMetrics()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var baselineId = Guid.NewGuid();
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.SimpleWorkList,
            [
                new MeasurementItemInput(first, "CON-01", "Concrete", "m3", 100m, true),
                new MeasurementItemInput(second, "REB-01", "Rebar", "kg", 200m, true)
            ],
            [
                new ProgressObservationInput(Guid.NewGuid(), first, new DateOnly(2026, 9, 10), ProgressReviewState.Approved, 50m),
                new ProgressObservationInput(Guid.NewGuid(), second, new DateOnly(2026, 9, 10), ProgressReviewState.Approved, 50m)
            ],
            new ApprovedPlanningBaselineInput(
                baselineId,
                "B-01",
                PlanningBaselineKind.MeasurementWeights,
                [
                    BasisEntry(first, PlanningEntryKind.MeasurementItem, ProgressMeasurementMethod.QuantityBased, 60m),
                    BasisEntry(second, PlanningEntryKind.MeasurementItem, ProgressMeasurementMethod.QuantityBased, 40m)
                ]),
            [],
            new DateOnly(2026, 9, 11));

        Assert.Equal(40m, calculation.OfficialOverallPhysicalPercent);
        Assert.Null(calculation.PlannedOverallPhysicalPercent);
        Assert.Null(calculation.ScheduleVariancePercent);
        Assert.Equal(OfficialProgressBasisState.Approved, calculation.OfficialProgressBasisState);
        Assert.Equal(ScheduleBasisState.NotConfigured, calculation.ScheduleBasisState);
    }

    [Fact]
    public void MissingApprovedActualKeepsWeightedOverallUnknown()
    {
        var itemId = Guid.NewGuid();
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.SimpleWorkList,
            [new MeasurementItemInput(itemId, "CON-01", "Concrete", "m3", 100m, true)],
            [new ProgressObservationInput(
                Guid.NewGuid(), itemId, new DateOnly(2026, 9, 11), ProgressReviewState.Provisional, 10m)],
            new ApprovedPlanningBaselineInput(
                Guid.NewGuid(),
                "B-01",
                PlanningBaselineKind.MeasurementWeights,
                [BasisEntry(itemId, PlanningEntryKind.MeasurementItem, ProgressMeasurementMethod.QuantityBased, 100m)]));

        Assert.Null(calculation.OfficialOverallPhysicalPercent);
        Assert.Equal(1, calculation.MissingActualEntryCount);
        Assert.Equal(OfficialProgressBasisState.IncompleteActualData, calculation.OfficialProgressBasisState);
    }

    [Fact]
    public void ApprovedScheduleCalculatesPlannedAndVarianceFromOfficialActual()
    {
        var itemId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.WbsBaseline,
            [new MeasurementItemInput(itemId, "CON-01", "Concrete", "m3", 100m, true)],
            [new ProgressObservationInput(
                Guid.NewGuid(), itemId, new DateOnly(2026, 9, 5), ProgressReviewState.Approved, 40m)],
            new ApprovedPlanningBaselineInput(
                Guid.NewGuid(),
                "B-01",
                PlanningBaselineKind.WbsBaseline,
                [
                    new PlanningBaselineEntryInput(
                        Guid.NewGuid(), "WBS-01", "Structure", PlanningEntryKind.Summary,
                        ProgressMeasurementMethod.None, null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), null, 1),
                    new PlanningBaselineEntryInput(
                        entryId, "ACT-01", "Concrete", PlanningEntryKind.Activity,
                        ProgressMeasurementMethod.QuantityBased, itemId,
                        new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), 100m, 2)
                ]),
            [],
            new DateOnly(2026, 9, 5));

        Assert.Equal(40m, calculation.OfficialOverallPhysicalPercent);
        Assert.Equal(50m, calculation.PlannedOverallPhysicalPercent);
        Assert.Equal(-10m, calculation.ScheduleVariancePercent);
        Assert.Null(calculation.ForecastCompletionDate);
        Assert.Equal(ScheduleBasisState.Approved, calculation.ScheduleBasisState);
    }

    [Fact]
    public void BaselineFromAnotherPlanningModeIsVisibleButCannotDriveMetrics()
    {
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.None,
            [],
            [],
            new ApprovedPlanningBaselineInput(
                Guid.NewGuid(),
                "B-01",
                PlanningBaselineKind.MilestonePlan,
                [new PlanningBaselineEntryInput(
                    Guid.NewGuid(), "M-01", "Handover", PlanningEntryKind.Milestone,
                    ProgressMeasurementMethod.ManualPercent, null,
                    new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), 100m, 1)]));

        Assert.Equal(OfficialProgressBasisState.ModeMismatch, calculation.OfficialProgressBasisState);
        Assert.Equal(ScheduleBasisState.ModeMismatch, calculation.ScheduleBasisState);
        Assert.Null(calculation.OfficialOverallPhysicalPercent);
        Assert.Null(calculation.ScheduleVariancePercent);
    }

    [Fact]
    public void MilestoneManualProgressRequiresEvidenceAndHumanReview()
    {
        var exception = Assert.Throws<DomainRuleException>(() => MilestoneProgressUpdate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 9, 11), 50m, "", null, Guid.NewGuid(), Now));
        Assert.Equal("planning.milestone_update.evidence.required", exception.Code);

        var update = MilestoneProgressUpdate.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 9, 11), 50m, "EVIDENCE-42", "Field minutes", Guid.NewGuid(), Now);
        update.Submit(1, Now.AddMinutes(1));
        update.Approve(2, "Reviewed", Guid.NewGuid(), Now.AddMinutes(2));

        Assert.Equal(MilestoneProgressStatus.Approved, update.Status);
        Assert.NotNull(update.ReviewedBy);
        Assert.Equal(3, update.Revision);
    }

    [Fact]
    public void BaselineRejectsIncompleteWeightsAndExternalScheduleWithoutSource()
    {
        var itemId = Guid.NewGuid();
        var weightException = Assert.Throws<DomainRuleException>(() => PlanningBaseline.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "B-01", "Basis",
            PlanningBaselineKind.MeasurementWeights, null, null,
            [new PlanningBaselineEntry(
                Guid.NewGuid(), null, "A", "A", PlanningEntryKind.MeasurementItem,
                ProgressMeasurementMethod.QuantityBased, itemId, null, null, 90m, null, 1)],
            Guid.NewGuid(), Now));
        Assert.Equal("planning.baseline.weights.invalid", weightException.Code);

        var sourceException = Assert.Throws<DomainRuleException>(() => PlanningBaseline.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "B-02", "External",
            PlanningBaselineKind.ExternalSchedule, null, null,
            [new PlanningBaselineEntry(
                Guid.NewGuid(), null, "A", "A", PlanningEntryKind.Activity,
                ProgressMeasurementMethod.QuantityBased, itemId,
                new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 100m, "EXT-1", 1)],
            Guid.NewGuid(), Now));
        Assert.Equal("planning.baseline.external_source.required", sourceException.Code);
    }

    [Fact]
    public void MilestoneBaselineAndApprovedUpdateProduceOfficialProgress()
    {
        var baselineId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.Milestones,
            [],
            [],
            new ApprovedPlanningBaselineInput(
                baselineId,
                "M-01",
                PlanningBaselineKind.MilestonePlan,
                [new PlanningBaselineEntryInput(
                    milestoneId, "M-01", "Handover", PlanningEntryKind.Milestone,
                    ProgressMeasurementMethod.ManualPercent, null,
                    new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 30), 100m, 1)]),
            [new ApprovedMilestoneProgressInput(
                baselineId, milestoneId, new DateOnly(2026, 9, 11), 70m, Now)],
            new DateOnly(2026, 10, 1));

        Assert.Equal(70m, calculation.OfficialOverallPhysicalPercent);
        Assert.Equal(100m, calculation.PlannedOverallPhysicalPercent);
        Assert.Equal(-30m, calculation.ScheduleVariancePercent);
        var milestone = Assert.Single(calculation.Milestones);
        Assert.Equal(MilestoneScheduleState.Late, milestone.ScheduleState);
    }

    [Fact]
    public void ProjectWorkingWeekControlsPlannedActivityDenominator()
    {
        var start = new DateOnly(2026, 9, 1);
        var itemId = Guid.NewGuid();
        var workingDaysMask = 1 << (int)start.DayOfWeek;
        var calculation = ProgressLedgerCalculator.Calculate(
            PlanningMode.WbsBaseline,
            [new MeasurementItemInput(itemId, "A-01", "Activity", "m", 100m, true)],
            [new ProgressObservationInput(Guid.NewGuid(), itemId, start, ProgressReviewState.Approved, 50m)],
            new ApprovedPlanningBaselineInput(
                Guid.NewGuid(),
                "B-01",
                PlanningBaselineKind.WbsBaseline,
                [new PlanningBaselineEntryInput(
                    Guid.NewGuid(), "A-01", "Activity", PlanningEntryKind.Activity,
                    ProgressMeasurementMethod.QuantityBased, itemId, start, start.AddDays(6), 100m, 1)]),
            [],
            start,
            workingDaysMask);

        Assert.Equal(100m, calculation.PlannedOverallPhysicalPercent);
        Assert.Equal(-50m, calculation.ScheduleVariancePercent);
    }

    [Fact]
    public void PlanningBaselineApprovalIsRevisionControlledAndVersionCodeRemainsStable()
    {
        var itemId = Guid.NewGuid();
        var baseline = PlanningBaseline.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "base-01", "Initial basis",
            PlanningBaselineKind.MeasurementWeights, null, null,
            [new PlanningBaselineEntry(
                Guid.NewGuid(), null, "A-01", "Activity", PlanningEntryKind.MeasurementItem,
                ProgressMeasurementMethod.QuantityBased, itemId, null, null, 100m, null, 1)],
            Guid.NewGuid(), Now);

        baseline.Amend(1, "Revised title", null, null, baseline.Entries);
        baseline.Submit(2, Now.AddMinutes(1));
        baseline.Approve(3, "Approved", Guid.NewGuid(), Now.AddMinutes(2));

        Assert.Equal("BASE-01", baseline.VersionCode);
        Assert.Equal("Revised title", baseline.Title);
        Assert.Equal(PlanningBaselineStatus.Approved, baseline.Status);
        Assert.Equal(4, baseline.Revision);
    }

    private static PlanningBaselineEntryInput BasisEntry(
        Guid measurementItemId,
        PlanningEntryKind kind,
        ProgressMeasurementMethod method,
        decimal weight) => new(
        Guid.NewGuid(),
        $"ITEM-{measurementItemId:N}",
        "Item",
        kind,
        method,
        measurementItemId,
        null,
        null,
        weight,
        1);
}
