using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.FieldOperations.Domain;

namespace Pmcs.Domain.Tests;

public sealed class StructuredDailyFactTests
{
    [Fact]
    public void MeasurementLinkedProgressRequiresQuantityAndUnit()
    {
        var input = new DailyFactInput(
            DailyFactKind.WorkProgress,
            "Measured work",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Guid.NewGuid());

        var exception = Assert.Throws<DomainRuleException>(() => CreateReport().AddFact(
            Guid.NewGuid(), input, Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.Equal("daily_fact.measurement_item.quantity.required", exception.Code);
    }

    [Fact]
    public void NonProgressFactCannotReferenceMeasurementItem()
    {
        var input = new DailyFactInput(
            DailyFactKind.Note,
            "Observed",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Guid.NewGuid());

        var exception = Assert.Throws<DomainRuleException>(() => CreateReport().AddFact(
            Guid.NewGuid(), input, Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.Equal("daily_fact.measurement_item.kind.invalid", exception.Code);
    }

    [Fact]
    public void ProgressFactDoesNotRequireWbsReference()
    {
        var report = CreateReport();

        var fact = report.AddFact(
            Guid.NewGuid(),
            new DailyFactInput(
                DailyFactKind.WorkProgress,
                "Concrete placement was measured on site.",
                "Concrete placement",
                "Level 3",
                42.5m,
                "m3",
                null,
                null,
                null,
                null),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Null(fact.ReferenceCode);
        Assert.Equal(42.5m, fact.Quantity);
    }

    [Fact]
    public void LaborFactRequiresTradeAndHeadcount()
    {
        var report = CreateReport();

        var exception = Assert.Throws<DomainRuleException>(() => report.AddFact(
            Guid.NewGuid(),
            new DailyFactInput(
                DailyFactKind.Labor,
                "Crew present",
                null,
                null,
                null,
                null,
                null,
                8m,
                null,
                null),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        Assert.Equal("daily_fact.resource.structure.required", exception.Code);
    }

    [Fact]
    public void HseIsNotRequiredForAnOperationalIssue()
    {
        var report = CreateReport();

        var fact = report.AddFact(
            Guid.NewGuid(),
            new DailyFactInput(
                DailyFactKind.Issue,
                "Approved drawing was not available.",
                "Document unavailable",
                "Level 3",
                null,
                null,
                null,
                null,
                DailyImpactLevel.High,
                "RFI-17"),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        Assert.Equal(DailyFactKind.Issue, fact.Kind);
        Assert.Equal(DailyImpactLevel.High, fact.ImpactLevel);
    }

    private static DailyReport CreateReport() => DailyReport.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 9, 9),
        null,
        null,
        Guid.NewGuid(),
        DateTimeOffset.UtcNow);
}
