namespace Pmcs.Modules.FieldOperations.Domain;

using Pmcs.BuildingBlocks.Domain;

public sealed class DailyReportFact
{
    private DailyReportFact()
    {
    }

    private DailyReportFact(
        Guid id,
        Guid dailyReportId,
        DailyFactInput input,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = id;
        DailyReportId = dailyReportId;
        Kind = input.Kind;
        Description = input.Description;
        Category = input.Category;
        LocationName = input.LocationName;
        Quantity = input.Quantity;
        Unit = input.Unit;
        ResourceCount = input.ResourceCount;
        Hours = input.Hours;
        ImpactLevel = input.ImpactLevel;
        ReferenceCode = input.ReferenceCode;
        MeasurementItemId = input.MeasurementItemId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid DailyReportId { get; private set; }

    public DailyFactKind Kind { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? Category { get; private set; }

    public string? LocationName { get; private set; }

    public decimal? Quantity { get; private set; }

    public string? Unit { get; private set; }

    public int? ResourceCount { get; private set; }

    public decimal? Hours { get; private set; }

    public DailyImpactLevel? ImpactLevel { get; private set; }

    public string? ReferenceCode { get; private set; }

    public Guid? MeasurementItemId { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static DailyReportFact Create(
        Guid id,
        Guid dailyReportId,
        DailyFactInput input,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        if (!Enum.IsDefined(input.Kind))
        {
            throw new DomainRuleException("daily_fact.kind.invalid", "Fact kind is invalid.");
        }

        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Trim().Length > 1_000)
        {
            throw new DomainRuleException("daily_fact.description.invalid", "Fact description is required and must be at most 1000 characters.");
        }

        if (input.Quantity is < 0)
        {
            throw new DomainRuleException("daily_fact.quantity.negative", "Fact quantity cannot be negative.");
        }

        if (input.Quantity.HasValue && string.IsNullOrWhiteSpace(input.Unit))
        {
            throw new DomainRuleException("daily_fact.unit.required", "A unit is required when quantity is provided.");
        }

        if (input.ResourceCount is <= 0)
        {
            throw new DomainRuleException("daily_fact.resource_count.invalid", "Resource count must be greater than zero.");
        }

        if (input.Hours is < 0 or > 100_000)
        {
            throw new DomainRuleException("daily_fact.hours.invalid", "Hours must be between zero and 100000.");
        }

        if (input.ImpactLevel.HasValue && !Enum.IsDefined(input.ImpactLevel.Value))
        {
            throw new DomainRuleException("daily_fact.impact.invalid", "Impact level is invalid.");
        }

        var category = NormalizeOptional(input.Category, 120, "daily_fact.category.too_long");
        if (input.Kind is DailyFactKind.Labor or DailyFactKind.Equipment &&
            (category is null || !input.ResourceCount.HasValue))
        {
            throw new DomainRuleException(
                "daily_fact.resource.structure.required",
                "Labor and equipment facts require a category and resource count.");
        }

        if (input.Kind == DailyFactKind.Material &&
            (category is null || !input.Quantity.HasValue || string.IsNullOrWhiteSpace(input.Unit)))
        {
            throw new DomainRuleException(
                "daily_fact.material.structure.required",
                "Material facts require a material name, quantity and unit.");
        }

        if (input.MeasurementItemId == Guid.Empty)
        {
            throw new DomainRuleException("daily_fact.measurement_item.invalid", "Measurement item id cannot be empty.");
        }

        if (input.MeasurementItemId.HasValue && input.Kind != DailyFactKind.WorkProgress)
        {
            throw new DomainRuleException(
                "daily_fact.measurement_item.kind.invalid",
                "Only progress facts can reference a measurement item.");
        }

        if (input.MeasurementItemId.HasValue &&
            (!input.Quantity.HasValue || string.IsNullOrWhiteSpace(input.Unit)))
        {
            throw new DomainRuleException(
                "daily_fact.measurement_item.quantity.required",
                "Measurement-linked progress requires quantity and unit.");
        }

        if (input.Kind == DailyFactKind.WorkProgress && category is null && !input.MeasurementItemId.HasValue)
        {
            throw new DomainRuleException(
                "daily_fact.progress.work_item.required",
                "Progress facts require a work-item name or measurement item; a WBS reference remains optional.");
        }

        var normalized = input with
        {
            Description = input.Description.Trim(),
            Category = category,
            LocationName = NormalizeOptional(input.LocationName, 200, "daily_fact.location.too_long"),
            Unit = NormalizeOptional(input.Unit, 40, "daily_fact.unit.too_long"),
            ReferenceCode = NormalizeOptional(input.ReferenceCode, 120, "daily_fact.reference.too_long")
        };

        return new DailyReportFact(id, dailyReportId, normalized, createdBy, createdAt);
    }

    private static string? NormalizeOptional(string? value, int maxLength, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainRuleException(errorCode, $"Value must be at most {maxLength} characters.");
        }

        return normalized;
    }
}

public enum DailyFactKind
{
    WorkProgress = 1,
    Labor = 2,
    Equipment = 3,
    Material = 4,
    Issue = 5,
    Stoppage = 6,
    SiteCondition = 7,
    Note = 8
}
