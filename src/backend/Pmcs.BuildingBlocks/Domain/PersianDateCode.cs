using System.Globalization;

namespace Pmcs.BuildingBlocks.Domain;

public static class PersianDateCode
{
    private static readonly PersianCalendar Calendar = new();
    private static readonly TimeZoneInfo TehranTimeZone = ResolveTehranTimeZone();

    public static string FromInstant(DateTimeOffset instant)
    {
        var localDate = TimeZoneInfo.ConvertTime(instant, TehranTimeZone).Date;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Calendar.GetYear(localDate):0000}{Calendar.GetMonth(localDate):00}{Calendar.GetDayOfMonth(localDate):00}");
    }

    private static TimeZoneInfo ResolveTehranTimeZone()
    {
        foreach (var id in new[] { "Asia/Tehran", "Iran Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Try the alternate IANA/Windows identifier.
            }
            catch (InvalidTimeZoneException)
            {
                // A broken host time-zone database must not silently produce a wrong official number.
            }
        }

        throw new InvalidOperationException("The Asia/Tehran time zone is required for Persian official numbering.");
    }
}
