using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;

namespace Pmcs.Modules.Reporting.Domain;

internal sealed record ProjectPeriodicReportSemanticSnapshot(
    string SchemaVersion,
    string DefinitionCode,
    string DefinitionVersion,
    ReportDataStatus DataStatus,
    IReadOnlyCollection<ProjectPeriodicReportReasonCode> ReasonCodes,
    ProjectPeriodicReportProjectIdentity Project,
    ProjectPeriodicReportPeriodIdentity Period,
    ProjectPeriodicReportCoverage Coverage,
    IReadOnlyCollection<ProjectPeriodicOfficialReport> OfficialReports,
    IReadOnlyCollection<ProjectPeriodicFactCount> FactCounts,
    IReadOnlyCollection<ProjectPeriodicQuantityTotal> QuantityTotals,
    IReadOnlyCollection<ProjectPeriodicResourceObservationTotal> ResourceObservationTotals,
    IReadOnlyCollection<ProjectPeriodicHighImpactFact> HighImpactFacts,
    string SourceManifestSha256);

internal sealed record ProjectPeriodicReportProjectIdentity(
    Guid Id,
    Guid TenantId,
    string Code,
    string Name,
    string TimeZone,
    long Revision,
    long ConfigurationVersion,
    DateTimeOffset ConfigurationChangedAt,
    ReportingFrequency ReportingFrequency,
    DailyReportWorkflow DailyReportWorkflow,
    TimeOnly? DailyCutoffLocalTime,
    ProjectCalendarConfigurationState CalendarState,
    int? WorkingDaysMask);

internal sealed record ProjectPeriodicReportPeriodIdentity(
    ProjectReportPeriodKind Kind,
    DateOnly StartLocalDate,
    DateOnly EndLocalDateExclusive,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtcExclusive,
    DateTimeOffset SourceCutoffUtc,
    bool ClosedAtCutoff);

internal sealed record ProjectPeriodicReportCoverage(
    int? ExpectedSlotCount,
    int? CoveredSlotCount,
    IReadOnlyCollection<DateOnly>? ExpectedDates,
    IReadOnlyCollection<DateOnly> OfficialReportDates,
    IReadOnlyCollection<DateOnly>? MissingExpectedDates,
    IReadOnlyCollection<ProjectPeriodicReportCoverageSlot>? Slots);

internal sealed record ProjectPeriodicReportCoverageSlot(
    DateOnly SlotDate,
    DateOnly StartLocalDate,
    DateOnly EndLocalDateExclusive,
    DateTimeOffset DueAtUtc,
    bool Covered,
    IReadOnlyCollection<DateOnly> OfficialReportDates);

internal sealed record ProjectPeriodicOfficialReport(
    DailyReportReportingClassification Classification,
    DailyReportReportingVersion Version);

internal sealed record ProjectPeriodicFactCount(
    DailyReportReportingFactKind Kind,
    int Count);

internal sealed record ProjectPeriodicQuantityTotal(
    DailyReportReportingFactKind Kind,
    string? SourceUnit,
    ProjectPeriodicReportUnitState UnitState,
    decimal Quantity);

internal sealed record ProjectPeriodicResourceObservationTotal(
    DailyReportReportingFactKind Kind,
    int? ResourceCount,
    decimal? Hours);

internal sealed record ProjectPeriodicHighImpactFact(
    DateOnly ReportDate,
    Guid RootReportId,
    Guid ReportId,
    DailyReportReportingFact Fact);
