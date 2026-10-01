using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.FieldOperations.Domain;
using Pmcs.Modules.FieldOperations.Services;

namespace Pmcs.Domain.Tests;

public sealed class DailyReportWorkflowTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 9, 9, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ObservedFactAdvancesRevisionAndCanBeSubmitted()
    {
        var report = CreateReport();

        report.AddFact(
            Guid.NewGuid(),
            Fact(DailyFactKind.Stoppage, "Electrical work stopped because the approved drawing was unavailable."),
            Guid.NewGuid(),
            StartedAt.AddMinutes(5));

        Assert.Equal(2L, report.Revision);
        Assert.Single(report.Facts);

        report.Submit(2L, StartedAt.AddMinutes(10));

        Assert.Equal(DailyReportStatus.Submitted, report.Status);
        Assert.Equal(3L, report.Revision);
    }

    [Fact]
    public void EmptyReportCannotBeSubmitted()
    {
        var report = CreateReport();

        var exception = Assert.Throws<DomainRuleException>(() => report.Submit(1L, StartedAt));

        Assert.Equal("daily_report.fact.required", exception.Code);
    }

    [Fact]
    public void StaleRevisionIsRejected()
    {
        var report = CreateReport();
        report.AddFact(
            Guid.NewGuid(),
            Fact(DailyFactKind.Note, "Observed fact"),
            Guid.NewGuid(),
            StartedAt.AddMinutes(5));

        var exception = Assert.Throws<DomainRuleException>(() => report.Submit(1L, StartedAt.AddMinutes(10)));

        Assert.Equal("daily_report.revision.conflict", exception.Code);
    }

    [Fact]
    public void ReturnedReportCanBeCorrectedAndResubmitted()
    {
        var report = CreateReport();
        report.AddFact(Guid.NewGuid(), Fact(DailyFactKind.Note, "Initial observation"), Guid.NewGuid(), StartedAt);
        report.Submit(2L, StartedAt.AddMinutes(1));

        report.ReturnForCorrection(3L, "Add the affected location.", Guid.NewGuid(), StartedAt.AddMinutes(2));
        report.AddFact(Guid.NewGuid(), Fact(DailyFactKind.Note, "Affected location: Level 3"), Guid.NewGuid(), StartedAt.AddMinutes(3));
        report.Submit(5L, StartedAt.AddMinutes(4));

        Assert.Equal(DailyReportStatus.Submitted, report.Status);
        Assert.Null(report.ReviewComment);
        Assert.Equal(6L, report.Revision);
    }

    [Fact]
    public void SubmittedReportCanBeApprovedWithRevisionCheck()
    {
        var reviewerId = Guid.NewGuid();
        var report = CreateReport();
        report.AddFact(Guid.NewGuid(), Fact(DailyFactKind.Note, "Observed fact"), Guid.NewGuid(), StartedAt);
        report.Submit(2L, StartedAt.AddMinutes(1));

        report.Approve(3L, "Reviewed against the site record.", reviewerId, StartedAt.AddMinutes(2));

        Assert.Equal(DailyReportStatus.Approved, report.Status);
        Assert.Equal(reviewerId, report.ReviewedBy);
        Assert.Equal(4L, report.Revision);
    }

    [Fact]
    public void ApprovedReportCreatesEditableCorrectionWithoutInvalidatingOfficialVersion()
    {
        var report = CreateApprovedReport();

        var correction = report.CreateCorrection(
            Guid.NewGuid(),
            report.Revision,
            "Correct the recorded quantity.",
            Guid.NewGuid(),
            StartedAt.AddMinutes(4));

        Assert.Equal(DailyReportStatus.Approved, report.Status);
        Assert.Equal(DailyReportStatus.Draft, correction.Status);
        Assert.Equal(report.RootReportId, correction.RootReportId);
        Assert.Equal(2, correction.VersionNumber);
        Assert.Equal(report.Id, correction.SupersedesReportId);
        Assert.Single(correction.Facts);
        Assert.Equal(report.Facts.Single().Id, correction.Facts.Single().CopiedFromFactId);
    }

    [Fact]
    public void ApprovedCorrectionSupersedesPredecessorWithBidirectionalLineage()
    {
        var report = CreateApprovedReport();
        var correction = report.CreateCorrection(
            Guid.NewGuid(), report.Revision, "Correct the quantity.", Guid.NewGuid(), StartedAt.AddMinutes(4));
        correction.Submit(correction.Revision, StartedAt.AddMinutes(5));
        correction.Approve(correction.Revision, null, Guid.NewGuid(), StartedAt.AddMinutes(6));

        report.SupersedeWith(correction.Id, correction.CorrectionReason!, StartedAt.AddMinutes(6));

        Assert.Equal(DailyReportStatus.Superseded, report.Status);
        Assert.Equal(correction.Id, report.SupersededByReportId);
        Assert.Equal(report.Id, correction.SupersedesReportId);
        Assert.Equal(DailyReportStatus.Approved, correction.Status);
    }

    [Fact]
    public void ReportingProjectionHidesFutureSupersessionMetadataAtHistoricalCutoff()
    {
        var report = CreateApprovedReport();
        var approvedAt = report.ReviewedAt!.Value;
        var approvedRevision = report.Revision;
        var correction = report.CreateCorrection(
            Guid.NewGuid(), report.Revision, "Correct the quantity.", Guid.NewGuid(), StartedAt.AddMinutes(4));
        correction.Submit(correction.Revision, StartedAt.AddMinutes(5));
        correction.Approve(correction.Revision, null, Guid.NewGuid(), StartedAt.AddMinutes(6));
        report.SupersedeWith(correction.Id, correction.CorrectionReason!, StartedAt.AddMinutes(6));

        var before = DailyReportReportingSource.MapForCutoff(report, StartedAt.AddMinutes(3));
        var after = DailyReportReportingSource.MapForCutoff(report, StartedAt.AddMinutes(6));

        Assert.Equal(DailyReportReportingVersionState.Approved, before.State);
        Assert.Null(before.SupersededByReportId);
        Assert.Null(before.SupersededAt);
        Assert.Null(before.CorrectionReason);
        Assert.Equal(approvedAt, before.LastModifiedAt);
        Assert.Equal(approvedRevision, before.Revision);

        Assert.Equal(DailyReportReportingVersionState.Superseded, after.State);
        Assert.Equal(correction.Id, after.SupersededByReportId);
        Assert.Equal(StartedAt.AddMinutes(6), after.SupersededAt);
        Assert.Equal("Correct the quantity.", after.CorrectionReason);
        Assert.Equal(approvedRevision + 1, after.Revision);
    }

    [Fact]
    public void CorrectionFactsCanBeRemovedButOriginalEvidenceRemainsImmutable()
    {
        var report = CreateApprovedReport();
        var originalFactId = report.Facts.Single().Id;
        var correction = report.CreateCorrection(
            Guid.NewGuid(), report.Revision, "Replace the fact.", Guid.NewGuid(), StartedAt.AddMinutes(4));
        var copiedFactId = correction.Facts.Single().Id;

        correction.RemoveFact(copiedFactId, correction.Revision, StartedAt.AddMinutes(5));

        Assert.Empty(correction.Facts);
        Assert.Equal(originalFactId, report.Facts.Single().Id);
    }

    [Fact]
    public void CorrectionRequiresReasonAndCurrentApprovedRevision()
    {
        var report = CreateApprovedReport();

        var missingReason = Assert.Throws<DomainRuleException>(() => report.CreateCorrection(
            Guid.NewGuid(), report.Revision, " ", Guid.NewGuid(), StartedAt.AddMinutes(4)));
        var staleRevision = Assert.Throws<DomainRuleException>(() => report.CreateCorrection(
            Guid.NewGuid(), report.Revision - 1, "Correct it.", Guid.NewGuid(), StartedAt.AddMinutes(4)));

        Assert.Equal("daily_report.correction.reason.invalid", missingReason.Code);
        Assert.Equal("daily_report.revision.conflict", staleRevision.Code);
    }

    private static DailyReport CreateReport() => DailyReport.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 9, 9),
        "Zone A",
        null,
        Guid.NewGuid(),
        StartedAt);

    private static DailyReport CreateApprovedReport()
    {
        var report = CreateReport();
        report.AddFact(Guid.NewGuid(), Fact(DailyFactKind.Note, "Observed fact"), Guid.NewGuid(), StartedAt);
        report.Submit(2, StartedAt.AddMinutes(1));
        report.Approve(3, null, Guid.NewGuid(), StartedAt.AddMinutes(2));
        return report;
    }

    private static DailyFactInput Fact(DailyFactKind kind, string description) => new(
        kind,
        description,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);
}
