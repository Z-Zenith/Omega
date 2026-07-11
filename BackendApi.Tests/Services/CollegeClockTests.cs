using BackendApi.Data.Entities;
using BackendApi.Services;

namespace BackendApi.Tests.Services;

public class CollegeClockTests
{
    private static College CollegeWithTimeZone(string timeZone) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test College",
        TimeZone = timeZone,
    };

    // #152 — a college ahead of UTC (e.g. IST, UTC+5:30) has already crossed into the next
    // calendar day locally while it's still the previous day in UTC.
    [Fact]
    public void LocalDate_RollsForwardAheadOfUtc_WhenUtcIsStillOnThePreviousDay()
    {
        var college = CollegeWithTimeZone("Asia/Kolkata");
        // 2026-07-09 19:00 UTC = 2026-07-10 00:30 IST — already the next day in Kolkata.
        var utcNow = new DateTime(2026, 7, 9, 19, 0, 0, DateTimeKind.Utc);

        var localDate = CollegeClock.LocalDate(college, utcNow);

        Assert.Equal(new DateOnly(2026, 7, 10), localDate);
    }

    // A college behind UTC (e.g. US Eastern) is still on the previous calendar day locally
    // while UTC has already rolled over — the mirror-image case of the above.
    [Fact]
    public void LocalDate_LagsBehindUtc_WhenLocalTimeIsStillOnThePreviousDay()
    {
        var college = CollegeWithTimeZone("America/New_York");
        // 2026-07-10 02:00 UTC = 2026-07-09 22:00 EDT — still the previous day in New York.
        var utcNow = new DateTime(2026, 7, 10, 2, 0, 0, DateTimeKind.Utc);

        var localDate = CollegeClock.LocalDate(college, utcNow);

        Assert.Equal(new DateOnly(2026, 7, 9), localDate);
    }

    [Fact]
    public void LocalDate_MatchesUtcDate_ForUtcCollege()
    {
        var college = CollegeWithTimeZone("UTC");
        var utcNow = new DateTime(2026, 7, 9, 23, 45, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 7, 9), CollegeClock.LocalDate(college, utcNow));
    }

    [Fact]
    public void LocalDate_FallsBackToUtc_WhenTimeZoneIdIsUnrecognized()
    {
        var college = CollegeWithTimeZone("Not/A/Real/Zone");
        var utcNow = new DateTime(2026, 7, 9, 23, 45, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 7, 9), CollegeClock.LocalDate(college, utcNow));
    }

    [Fact]
    public void LocalDate_FallsBackToUtc_WhenCollegeIsNull()
    {
        var utcNow = new DateTime(2026, 7, 9, 23, 45, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 7, 9), CollegeClock.LocalDate(null, utcNow));
    }
}
