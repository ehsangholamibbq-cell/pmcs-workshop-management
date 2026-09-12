using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Domain;

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

    private static DailyReport CreateReport() => DailyReport.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 9, 9),
        "Zone A",
        null,
        Guid.NewGuid(),
        StartedAt);

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
