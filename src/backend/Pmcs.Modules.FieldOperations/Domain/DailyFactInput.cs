namespace Pmcs.Modules.FieldOperations.Domain;

public sealed record DailyFactInput(
    DailyFactKind Kind,
    string Description,
    string? Category,
    string? LocationName,
    decimal? Quantity,
    string? Unit,
    int? ResourceCount,
    decimal? Hours,
    DailyImpactLevel? ImpactLevel,
    string? ReferenceCode,
    Guid? MeasurementItemId = null);

public enum DailyImpactLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
