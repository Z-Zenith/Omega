using BackendApi.Data.Entities;

namespace BackendApi.Services;

// #152 - derives "today"/session dates from a college's local time zone instead of raw UTC,
// which was silently rolling attendance session dates (and fee due-date checks) over to the
// wrong calendar day near local midnight for any college not on UTC.
public static class CollegeClock
{
    public static DateOnly LocalDate(College? college, DateTime utcNow)
    {
        var timeZone = ResolveTimeZone(college?.TimeZone ?? "UTC");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZone);
        return DateOnly.FromDateTime(localNow);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Malformed/unrecognized data shouldn't crash the request - fall back to UTC,
            // which is the same behavior every caller had before this fix existed.
            return TimeZoneInfo.Utc;
        }
    }
}
