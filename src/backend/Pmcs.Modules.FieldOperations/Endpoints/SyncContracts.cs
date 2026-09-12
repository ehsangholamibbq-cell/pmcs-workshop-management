using Pmcs.Modules.FieldOperations.Domain;

namespace Pmcs.Modules.FieldOperations.Endpoints;

public sealed record CaptureDailyReportFactPayload(
    Guid FactId,
    DateOnly ReportDate,
    string? LocationName,
    DailyFactKind Kind,
    string Description,
    string? Category,
    string? FactLocationName,
    decimal? Quantity,
    string? Unit,
    int? ResourceCount,
    decimal? Hours,
    DailyImpactLevel? ImpactLevel,
    string? ReferenceCode,
    Guid? MeasurementItemId = null);
