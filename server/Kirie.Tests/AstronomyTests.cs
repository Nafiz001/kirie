using Kirie.Api.Sky;

namespace Kirie.Tests;

public class AstronomyTests
{
    private static DateTimeOffset LocalMidnight(int year, int month, int day, string zone)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var date = new DateTime(year, month, day);
        return new DateTimeOffset(date, tz.GetUtcOffset(date));
    }

    private static void Near(DateTimeOffset? actual, string expectedLocal, TimeZoneInfo zone, int minutes = 3)
    {
        Assert.NotNull(actual);
        var local = TimeZoneInfo.ConvertTime(actual.Value, zone);
        var expected = TimeSpan.Parse(expectedLocal);
        Assert.InRange(Math.Abs((local.TimeOfDay - expected).TotalMinutes), 0, minutes);
    }

    [Fact]
    public void Sunrise_and_sunset_in_London_on_the_summer_solstice()
    {
        // Published times: sunrise 04:43, sunset 21:21 BST.
        var (rise, set) = Astronomy.SunEvents(LocalMidnight(2024, 6, 21, "Europe/London"), 51.5072, -0.1276);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        Near(rise, "04:43", zone);
        Near(set, "21:21", zone);
    }

    [Fact]
    public void Sunrise_and_sunset_in_New_York_on_the_winter_solstice()
    {
        // Published times: sunrise 07:16, sunset 16:32 EST.
        var (rise, set) = Astronomy.SunEvents(LocalMidnight(2024, 12, 21, "America/New_York"), 40.7128, -74.0060);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Near(rise, "07:16", zone);
        Near(set, "16:32", zone);
    }

    [Fact]
    public void The_sun_never_rises_in_Tromso_at_midwinter_and_never_sets_at_midsummer()
    {
        var winter = Astronomy.SunEvents(LocalMidnight(2024, 12, 21, "Europe/Oslo"), 69.6492, 18.9553);
        Assert.Null(winter.Rise);
        Assert.Null(winter.Set);
        Assert.True(Astronomy.Sun(new DateTimeOffset(2024, 12, 21, 11, 0, 0, TimeSpan.Zero), 69.6492, 18.9553).Altitude < 0);

        var summer = Astronomy.SunEvents(LocalMidnight(2024, 6, 21, "Europe/Oslo"), 69.6492, 18.9553);
        Assert.Null(summer.Rise);
        Assert.Null(summer.Set);
        Assert.True(Astronomy.Sun(new DateTimeOffset(2024, 6, 20, 23, 0, 0, TimeSpan.Zero), 69.6492, 18.9553).Altitude > 0);
    }

    [Fact]
    public void The_sun_is_nearly_overhead_at_the_equator_at_an_equinox_noon()
    {
        // 2024 March equinox; solar noon at Greenwich is around 12:07 UTC.
        var sun = Astronomy.Sun(new DateTimeOffset(2024, 3, 20, 12, 7, 0, TimeSpan.Zero), 0, 0);
        Assert.InRange(sun.Altitude, 88.5, 90);

        var (latitude, longitude) = Astronomy.Subsolar(new DateTimeOffset(2024, 3, 20, 12, 7, 0, TimeSpan.Zero));
        Assert.InRange(latitude, -0.5, 0.5);
        Assert.InRange(longitude, -1, 1);
    }

    [Fact]
    public void The_morning_sun_is_in_the_east_and_the_evening_sun_in_the_west()
    {
        var morning = Astronomy.Sun(new DateTimeOffset(2024, 3, 20, 7, 0, 0, TimeSpan.Zero), 51.5, 0);
        var evening = Astronomy.Sun(new DateTimeOffset(2024, 3, 20, 17, 0, 0, TimeSpan.Zero), 51.5, 0);
        Assert.InRange(morning.Azimuth, 45, 135);
        Assert.InRange(evening.Azimuth, 225, 315);
    }

    [Fact]
    public void Moon_phases_match_known_new_and_full_moons()
    {
        // New moon 2024-04-08 18:21 UTC (the total eclipse); full moon 2024-04-23 23:49 UTC.
        var newMoon = Astronomy.MoonIllumination(new DateTimeOffset(2024, 4, 8, 18, 21, 0, TimeSpan.Zero));
        var fullMoon = Astronomy.MoonIllumination(new DateTimeOffset(2024, 4, 23, 23, 49, 0, TimeSpan.Zero));
        var firstQuarter = Astronomy.MoonIllumination(new DateTimeOffset(2024, 4, 15, 19, 13, 0, TimeSpan.Zero));

        Assert.InRange(newMoon.Illumination, 0, 0.02);
        Assert.InRange(fullMoon.Illumination, 0.98, 1);
        Assert.InRange(fullMoon.Phase, 0.46, 0.54);
        Assert.InRange(firstQuarter.Illumination, 0.42, 0.58);
        Assert.True(firstQuarter.Waxing);
    }
}
