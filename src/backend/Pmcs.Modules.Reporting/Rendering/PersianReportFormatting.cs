using System.Globalization;
using System.Text;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.Reporting.Domain;

namespace Pmcs.Modules.Reporting.Rendering;

internal static class PersianReportFormatting
{
    private static readonly PersianCalendar Calendar = new();
    private static readonly char[] PersianDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];

    public static string ToPersianDigits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var index = 0; index < source.Length; index++)
            {
                var character = source[index];
                span[index] = character is >= '0' and <= '9'
                    ? PersianDigits[character - '0']
                    : character;
            }
        });
    }

    public static string FormatDate(DateOnly value, bool persianDigits = true)
    {
        var date = value.ToDateTime(TimeOnly.MinValue);
        var formatted = $"{Calendar.GetYear(date):0000}/{Calendar.GetMonth(date):00}/{Calendar.GetDayOfMonth(date):00}";
        return persianDigits ? ToPersianDigits(formatted) : formatted;
    }

    public static string FormatInstant(DateTimeOffset utcValue, string timeZoneId)
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw InvalidTimeZone(exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw InvalidTimeZone(exception);
        }

        var local = TimeZoneInfo.ConvertTime(utcValue.ToUniversalTime(), timeZone);
        var date = local.DateTime;
        var formatted = $"{Calendar.GetYear(date):0000}/{Calendar.GetMonth(date):00}/{Calendar.GetDayOfMonth(date):00} " +
            $"{date.Hour:00}:{date.Minute:00}";
        return ToPersianDigits(formatted);
    }

    public static string FormatDecimal(decimal? value) => value.HasValue
        ? ToPersianDigits(value.Value.ToString("0.############################", CultureInfo.InvariantCulture))
        : "—";

    public static string DataStatus(ReportDataStatus status) => status switch
    {
        ReportDataStatus.Available => "داده رسمی موجود",
        ReportDataStatus.NoData => "داده رسمی وجود ندارد",
        ReportDataStatus.InsufficientData => "داده رسمی ناکافی است",
        ReportDataStatus.NotConfigured => "منبع داده پیکربندی نشده است",
        _ => "وضعیت نامشخص"
    };

    public static string VersionState(DailyReportReportingVersionState state) => state switch
    {
        DailyReportReportingVersionState.Approved => "نسخه رسمی جاری",
        DailyReportReportingVersionState.Superseded => "نسخه رسمی جایگزین‌شده",
        _ => "نامشخص"
    };

    public static string FactKind(DailyReportReportingFactKind kind) => kind switch
    {
        DailyReportReportingFactKind.WorkProgress => "پیشرفت کار",
        DailyReportReportingFactKind.Labor => "نیروی انسانی",
        DailyReportReportingFactKind.Equipment => "ماشین‌آلات",
        DailyReportReportingFactKind.Material => "مصالح",
        DailyReportReportingFactKind.Issue => "مسئله",
        DailyReportReportingFactKind.Stoppage => "توقف",
        DailyReportReportingFactKind.SiteCondition => "شرایط کارگاه",
        DailyReportReportingFactKind.Note => "یادداشت",
        _ => "نامشخص"
    };

    public static string Impact(DailyReportReportingImpactLevel? impact) => impact switch
    {
        DailyReportReportingImpactLevel.Low => "کم",
        DailyReportReportingImpactLevel.Medium => "متوسط",
        DailyReportReportingImpactLevel.High => "زیاد",
        DailyReportReportingImpactLevel.Critical => "بحرانی",
        _ => "—"
    };

    public static string SafeText(string? value, int maximumLength = 4_000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        var builder = new StringBuilder(Math.Min(value.Length, maximumLength));
        foreach (var character in value.Trim())
        {
            if (builder.Length >= maximumLength)
            {
                break;
            }

            if (!char.IsControl(character) || character is '\n' or '\r' or '\t')
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? "—" : builder.ToString();
    }

    public static string SafeSpreadsheetText(string? value, int maximumLength = 32_000)
    {
        var normalized = SafeText(value, maximumLength);
        return normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@'
            ? $"'{normalized}"
            : normalized;
    }

    private static ReportRenderingException InvalidTimeZone(Exception exception) => new(
        "reporting.project.time_zone.invalid",
        transient: false,
        "The project time zone cannot be used for certified rendering.",
        exception);
}
