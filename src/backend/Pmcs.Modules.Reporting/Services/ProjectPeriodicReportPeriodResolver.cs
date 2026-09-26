using System.Globalization;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Services;

internal static class ProjectPeriodicReportPeriodResolver
{
    public static ResolvedProjectReportPeriod Resolve(
        ProjectPeriodicReportParameters parameters,
        string projectTimeZone,
        DateTimeOffset asOfUtc,
        DateTimeOffset validatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (!Enum.IsDefined(parameters.PeriodKind))
        {
            throw new DomainRuleException(
                "reporting.period.kind.invalid",
                "The report period kind must be Weekly or Monthly.");
        }

        var timeZone = ResolveTimeZone(projectTimeZone);
        var periodEndLocalDateExclusive = parameters.PeriodKind switch
        {
            ProjectReportPeriodKind.Weekly => ResolveWeeklyEnd(parameters.PeriodStartLocalDate),
            ProjectReportPeriodKind.Monthly => ResolveMonthlyEnd(parameters.PeriodStartLocalDate),
            _ => throw new DomainRuleException(
                "reporting.period.kind.invalid",
                "The report period kind must be Weekly or Monthly.")
        };
        var periodStartUtc = ResolveLocalInstantUtc(
            parameters.PeriodStartLocalDate,
            TimeOnly.MinValue,
            timeZone);
        var periodEndUtcExclusive = ResolveLocalInstantUtc(
            periodEndLocalDateExclusive,
            TimeOnly.MinValue,
            timeZone);
        var normalizedAsOf = asOfUtc.ToUniversalTime();
        var normalizedValidationTime = validatedAtUtc.ToUniversalTime();
        if (normalizedAsOf > normalizedValidationTime)
        {
            throw new DomainRuleException(
                "reporting.period.cutoff.future",
                "The source cutoff cannot be in the future at validation time.");
        }

        if (normalizedAsOf < periodStartUtc)
        {
            throw new DomainRuleException(
                "reporting.period.cutoff.before_start",
                "The source cutoff cannot precede the reporting period.");
        }

        return new ResolvedProjectReportPeriod(
            parameters.PeriodKind,
            parameters.PeriodStartLocalDate,
            periodEndLocalDateExclusive,
            periodStartUtc,
            periodEndUtcExclusive,
            normalizedAsOf,
            normalizedAsOf >= periodEndUtcExclusive,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(normalizedAsOf, timeZone).Date),
            timeZone);
    }

    internal static DateTimeOffset ResolveLocalInstantUtc(
        DateOnly date,
        TimeOnly time,
        TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local) || timeZone.IsAmbiguousTime(local))
        {
            throw new DomainRuleException(
                "reporting.period.local_boundary.invalid",
                "A reporting boundary is invalid or ambiguous in the pinned project time zone.");
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveTimeZone(string projectTimeZone)
    {
        if (string.IsNullOrWhiteSpace(projectTimeZone))
        {
            throw new DomainRuleException(
                "reporting.period.time_zone.invalid",
                "A pinned project time zone is required.");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(projectTimeZone.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            throw new DomainRuleException(
                "reporting.period.time_zone.invalid",
                "The pinned project time zone is unknown.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new DomainRuleException(
                "reporting.period.time_zone.invalid",
                "The pinned project time zone is invalid.");
        }
    }

    private static DateOnly ResolveWeeklyEnd(DateOnly periodStartLocalDate)
    {
        if (periodStartLocalDate.DayOfWeek != DayOfWeek.Saturday)
        {
            throw new DomainRuleException(
                "reporting.period.start.invalid",
                "A weekly report must start on Saturday in project local time.");
        }

        return periodStartLocalDate.AddDays(7);
    }

    private static DateOnly ResolveMonthlyEnd(DateOnly periodStartLocalDate)
    {
        try
        {
            var persianCalendar = new PersianCalendar();
            var localStart = periodStartLocalDate.ToDateTime(TimeOnly.MinValue);
            if (persianCalendar.GetDayOfMonth(localStart) != 1)
            {
                throw new DomainRuleException(
                    "reporting.period.start.invalid",
                    "A monthly report must start on the first day of a Persian month.");
            }

            return DateOnly.FromDateTime(persianCalendar.AddMonths(localStart, 1));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new DomainRuleException(
                "reporting.period.start.invalid",
                "The monthly report start is outside the supported Persian calendar range.");
        }
    }
}

internal sealed record ResolvedProjectReportPeriod(
    ProjectReportPeriodKind PeriodKind,
    DateOnly PeriodStartLocalDate,
    DateOnly PeriodEndLocalDateExclusive,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtcExclusive,
    DateTimeOffset SourceCutoffUtc,
    bool PeriodClosedAtCutoff,
    DateOnly CutoffLocalDate,
    TimeZoneInfo TimeZone);
