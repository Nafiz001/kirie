using Kirie.Api.Cities;
using Kirie.Api.Scenes;
using Kirie.Api.Weather;

namespace Kirie.Tests;

public class ColourTests
{
    [Theory]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("#3f86e0")]
    [InlineData("#ff7a4d")]
    public void Hex_round_trips(string hex) => Assert.Equal(hex, Colour.Hex(hex).ToHex());

    [Fact]
    public void Mixing_returns_the_ends_and_stays_in_gamut()
    {
        var a = Colour.Hex("#0b1026");
        var b = Colour.Hex("#ffb26b");
        Assert.Equal("#0b1026", Colour.Mix(a, b, 0).ToHex());
        Assert.Equal("#ffb26b", Colour.Mix(a, b, 1).ToHex());
        Assert.Matches("^#[0-9a-f]{6}$", Colour.Mix(a, b, 0.5).ToHex());
    }
}

public class WmoTests
{
    [Fact]
    public void Every_code_open_meteo_reports_has_a_name_and_a_look()
    {
        int[] codes = [0, 1, 2, 3, 45, 48, 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, 99];
        foreach (var code in codes)
            Assert.True(Wmo.IsKnown(code), $"code {code}");
        Assert.Equal(Precipitation.Rain, Wmo.Kind(63));
        Assert.Equal(Precipitation.Snow, Wmo.Kind(85));
        Assert.Equal(Precipitation.Hail, Wmo.Kind(99));
        Assert.Equal(Precipitation.None, Wmo.Kind(3));
    }
}

public class AtlasTests
{
    [Fact]
    public void Every_place_has_a_resolvable_time_zone_and_a_unique_id()
    {
        var atlas = new Atlas();
        Assert.Equal(atlas.All.Count, atlas.All.Select(c => c.Id).Distinct().Count());
        foreach (var city in atlas.All)
        {
            Assert.NotNull(city.Zone);
            Assert.NotEmpty(city.Terrain);
            Assert.InRange(city.Latitude, -90, 90);
        }
    }

    [Fact]
    public void Pages_run_west_to_east()
    {
        var longitudes = new Atlas().All.Select(c => c.Longitude).ToArray();
        Assert.Equal(longitudes.Order(), longitudes);
    }
}

public class SceneComposerTests
{
    private static readonly SceneComposer Composer = new();
    private static readonly City London = new Atlas().Find("london")!;
    private static readonly City Tromso = new Atlas().Find("tromso")!;

    private static WeatherReading Reading(int code, double cloud = 0, double precipitation = 0, double visibility = 24_000, double wind = 5) => new()
    {
        CityId = "london",
        Source = "test",
        Code = code,
        CloudCover = cloud,
        PrecipitationMm = precipitation,
        VisibilityM = visibility,
        WindKph = wind,
        GustKph = wind,
        TemperatureC = 12,
        Humidity = 60,
    };

    [Theory]
    [InlineData(40, "day")]
    [InlineData(2, "golden-hour")]
    [InlineData(-3, "civil-twilight")]
    [InlineData(-9, "nautical-twilight")]
    [InlineData(-15, "astronomical-twilight")]
    [InlineData(-40, "night")]
    public void Phases_follow_the_sun(double altitude, string phase) => Assert.Equal(phase, SceneComposer.PhaseFor(altitude));

    [Fact]
    public void Rain_falls_and_greys_the_sky()
    {
        var effects = SceneComposer.EffectsFor(Reading(63, cloud: 95, precipitation: 3.2), 30);
        Assert.Equal("rain", effects.Precipitation);
        Assert.InRange(effects.Intensity, 0.5, 1);
        Assert.True(effects.Clouds >= 0.9);
        Assert.False(effects.Lightning);
    }

    [Fact]
    public void A_rain_code_with_nothing_measured_still_rains()
    {
        var effects = SceneComposer.EffectsFor(Reading(61, cloud: 80, precipitation: 0), 30);
        Assert.Equal("rain", effects.Precipitation);
        Assert.True(effects.Intensity > 0);
    }

    [Fact]
    public void Fog_comes_from_visibility_and_thunder_brings_lightning()
    {
        Assert.InRange(SceneComposer.EffectsFor(Reading(3, visibility: 300), 10).Fog, 0.9, 1);
        Assert.Equal(0, SceneComposer.EffectsFor(Reading(0, visibility: 30_000), 10).Fog);
        Assert.True(SceneComposer.EffectsFor(Reading(95, cloud: 100, precipitation: 6), 10).Lightning);
    }

    [Fact]
    public void Clear_nights_show_stars_and_cloudy_nights_hide_them()
    {
        var midnight = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
        var clear = Composer.Compose(London, Reading(0), midnight, [], 0);
        var cloudy = Composer.Compose(London, Reading(3, cloud: 100), midnight, [], 0);

        Assert.Equal("night", clear.Phase);
        Assert.True(clear.Sky.Stars > 0.8);
        Assert.True(cloudy.Sky.Stars < 0.05);
        Assert.True(clear.Effects.WindowsLit > 0.9);
        Assert.True(clear.Sky.Light < 0.35);
    }

    [Fact]
    public void Noon_is_bright_and_blue()
    {
        var noon = Composer.Compose(London, Reading(0), new DateTimeOffset(2024, 6, 21, 12, 0, 0, TimeSpan.Zero), [], 0);
        Assert.Equal("day", noon.Phase);
        Assert.True(noon.Sky.Light > 0.95);
        Assert.Equal(0, noon.Sky.Stars);
        var top = Colour.Hex(noon.Sky.Top);
        Assert.True(top.B > top.R, "a clear noon sky is bluer than it is red");
    }

    [Fact]
    public void The_summary_notices_the_midnight_sun_and_the_polar_night()
    {
        var june = Composer.Compose(Tromso, Reading(0), new DateTimeOffset(2024, 6, 20, 22, 30, 0, TimeSpan.Zero), [], 0);
        Assert.True(june.Sun.AlwaysUp);
        Assert.Null(june.Sun.Sunset);
        Assert.Equal("Clear sky under the midnight sun", june.Summary);

        var december = Composer.Compose(Tromso, Reading(3, cloud: 100), new DateTimeOffset(2024, 12, 21, 11, 0, 0, TimeSpan.Zero), [], 0);
        Assert.True(december.Sun.AlwaysDown);
        Assert.Equal("Overcast in the polar night", december.Summary);
    }

    [Fact]
    public void Scene_clock_and_sun_times_are_local()
    {
        var scene = Composer.Compose(London, Reading(0), new DateTimeOffset(2024, 6, 21, 11, 0, 0, TimeSpan.Zero), [], 0);
        Assert.Equal("12:00", scene.Clock);
        Assert.StartsWith("04:4", scene.Sun.Sunrise);
        Assert.Equal("test", scene.Source);
    }
}
