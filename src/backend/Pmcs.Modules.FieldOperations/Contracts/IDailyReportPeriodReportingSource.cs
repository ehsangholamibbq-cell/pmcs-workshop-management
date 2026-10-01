namespace Pmcs.Modules.FieldOperations.Contracts;

public interface IDailyReportPeriodReportingSource
{
    Task<DailyReportReportingPeriod> LoadPeriodAsync(
        Guid tenantId,
        Guid projectId,
        DateOnly periodStartLocalDate,
        DateOnly periodEndLocalDateExclusive,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);
}

public static class DailyReportPeriodReportingContract
{
    public const string Version = "pmcs.field-operations.daily-report-period/v1";
}

public sealed record DailyReportReportingPeriod(
    string ContractVersion,
    Guid TenantId,
    Guid ProjectId,
    DateOnly PeriodStartLocalDate,
    DateOnly PeriodEndLocalDateExclusive,
    DateTimeOffset AsOfUtc,
    IReadOnlyCollection<DailyReportReportingRoot> Roots);

public sealed record DailyReportReportingRoot(
    Guid RootReportId,
    DateOnly ReportDate,
    Guid? CurrentOfficialReportId,
    DailyReportReportingClassification Classification,
    IReadOnlyCollection<DailyReportReportingVersion> Versions);

public enum DailyReportReportingClassification
{
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}
