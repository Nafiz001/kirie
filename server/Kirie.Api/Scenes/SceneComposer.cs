using System.Globalization;
using Kirie.Api.Cities;
using Kirie.Api.Sky;
using Kirie.Api.Weather;

namespace Kirie.Api.Scenes;

/// <summary>
/// Turns a place, its weather and the moment into a page: sky colours from the
/// sun's altitude, greyed by cloud; how brightly the paper is lit; what falls,
/// drifts and flashes.
/// </summary>
public sealed class SceneComposer
{
    // Clear-sky colours (top, middle, horizon) by the sun's altitude in degrees.
    private static readonly (double Altitude, Colour Top, Colour Middle, Colour Horizon)[] ClearSky =
    [
        (-90, Colour.Hex("#070b1c"), Colour.Hex("#0d1430"), Colour.Hex("#151f42")),
        (-18, Colour.Hex("#0b1026"), Colour.Hex("#141c3d"), Colour.Hex("#1d2a4f")),
        (-12, Colour.Hex("#111b40"), Colour.Hex("#1f2f5e"), Colour.Hex("#33426f")),
        (-6, Colour.Hex("#213268"), Colour.Hex("#4a5690"), Colour.Hex("#8d7299")),
        (-2, Colour.Hex("#3a5a9c"), Colour.Hex("#8a79b0"), Colour.Hex("#ef8a63")),
        (1, Colour.Hex("#5580c0"), Colour.Hex("#d99a8e"), Colour.Hex("#ffb26b")),
        (6, Colour.Hex("#4d8ad6"), Colour.Hex("#93bfe6"), Colour.Hex("#f5d39c")),
        (20, Colour.Hex("#3f86e0"), Colour.Hex("#7fb6ee"), Colour.Hex("#cfe6f7")),
        (90, Colour.Hex("#2f78de"), Colour.Hex("#69aaf0"), Colour.Hex("#bddff9")),
    ];

    private static readonly (Colour Top, Colour Middle, Colour Horizon) OvercastDay =
        (Colour.Hex("#8e99a6"), Colour.Hex("#aab3bc"), Colour.Hex("#c5cbd1"));

    private static readonly (Colour Top, Colour Middle, Colour Horizon) OvercastNight =
        (Colour.Hex("#101218"), Colour.Hex("#161920"), Colour.Hex("#1f232b"));

    private static readonly Colour StormGrey = Colour.Hex("#2b303a");
    private static readonly Colour EveningGlow = Colour.Hex("#ff7a4d");

    public Scene Compose(City city, WeatherReading? weather, DateTimeOffset now, CraneView[] cranes, int craneCount)
    {
        var local = TimeZoneInfo.ConvertTime(now, city.Zone);
        var sun = Astronomy.Sun(now, city.Latitude, city.Longitude);
        var rising = Astronomy.Sun(now.AddMinutes(10), city.Latitude, city.Longitude).Altitude > sun.Altitude;
        var moon = Astronomy.Moon(now, city.Latitude, city.Longitude);
        var moonLight = Astronomy.MoonIllumination(now);

        var midnight = new DateTimeOffset(local.Date, city.Zone.GetUtcOffset(local.Date));
        var (rise, set) = Astronomy.SunEvents(midnight, city.Latitude, city.Longitude);
        var noon = Astronomy.Sun(midnight.AddHours(12), city.Latitude, city.Longitude).Altitude;
        var alwaysUp = rise is null && set is null && noon > Astronomy.HorizonAltitude;
        var alwaysDown = rise is null && set is null && noon <= Astronomy.HorizonAltitude;

        var code = weather?.Code ?? 0;
        var effects = EffectsFor(weather, sun.Altitude);
        var sky = SkyFor(sun.Altitude, rising, weather, effects, moon, moonLight);

        return new Scene(
            CityView.From(city),
            local.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            local.ToString("HH:mm", CultureInfo.InvariantCulture),
            PhaseFor(sun.Altitude),
            rising,
            Summarise(code, sun.Altitude, rising, local, alwaysUp, alwaysDown),
            new Conditions(
                code,
                Wmo.Describe(code),
                weather?.TemperatureC,
                weather?.Humidity,
                weather?.CloudCover,
                weather?.PrecipitationMm,
                weather?.WindKph,
                weather?.WindDirection,
                weather?.VisibilityM),
            new SunView(
                Math.Round(sun.Altitude, 2),
                Math.Round(sun.Azimuth, 2),
                rise is null ? null : Clock(rise.Value, city),
                set is null ? null : Clock(set.Value, city),
                alwaysUp,
                alwaysDown),
            new MoonView(
                Math.Round(moon.Altitude, 2),
                Math.Round(moon.Azimuth, 2),
                Math.Round(moonLight.Illumination, 3),
                Math.Round(moonLight.Phase, 3),
                MoonName(moonLight.Phase)),
            sky,
            effects,
            weather?.ObservedAt,
            weather?.Source ?? "none",
            cranes,
            craneCount);
    }

    public CityCard Card(City city, WeatherReading? weather, DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, city.Zone);
        var sun = Astronomy.Sun(now, city.Latitude, city.Longitude);
        var rising = Astronomy.Sun(now.AddMinutes(10), city.Latitude, city.Longitude).Altitude > sun.Altitude;
        var code = weather?.Code ?? 0;
        return new CityCard(
            city.Id,
            city.Name,
            city.Country,
            city.Latitude,
            city.Longitude,
            local.ToString("HH:mm", CultureInfo.InvariantCulture),
            PhaseFor(sun.Altitude),
            Summarise(code, sun.Altitude, rising, local, alwaysUp: false, alwaysDown: false),
            weather?.TemperatureC,
            code);
    }

    public static string PhaseFor(double sunAltitude) => sunAltitude switch
    {
        >= 6 => "day",
        >= Astronomy.HorizonAltitude => "golden-hour",
        >= -6 => "civil-twilight",
        >= -12 => "nautical-twilight",
        >= -18 => "astronomical-twilight",
        _ => "night",
    };

    internal static Effects EffectsFor(WeatherReading? weather, double sunAltitude)
    {
        if (weather is null)
            return new Effects(0.1, 0.1, "none", 0, 0, 0.1, 270, false, WindowsLit(sunAltitude, 0));

        var kind = Wmo.Kind(weather.Code);
        if (kind == Precipitation.None && weather.SnowfallCm > 0) kind = Precipitation.Snow;

        var measured = Math.Log(1 + weather.PrecipitationMm) / Math.Log(1 + 8);
        var intensity = kind == Precipitation.None ? 0 : Math.Clamp(Math.Max(measured, Wmo.NominalIntensity(weather.Code) * 0.8), 0.05, 1);

        var fog = weather.VisibilityM < 5000 ? 1 - (weather.VisibilityM - 200) / 4800 : 0;
        if (Wmo.IsFog(weather.Code)) fog = Math.Max(fog, 0.7);
        if (weather.Humidity > 90 && weather.VisibilityM < 10_000) fog += 0.1;

        var thunder = Wmo.IsThunder(weather.Code);
        var clouds = Math.Clamp(weather.CloudCover / 100, 0, 1);
        if (kind != Precipitation.None) clouds = Math.Max(clouds, 0.6);
        var darkness = Math.Clamp(0.12 + intensity * 0.45 + (thunder ? 0.3 : 0) + clouds * 0.15, 0, 1);

        return new Effects(
            Round(clouds),
            Round(darkness),
            kind.ToString().ToLowerInvariant(),
            Round(intensity),
            Round(Math.Clamp(fog, 0, 1)),
            Round(Math.Clamp(Math.Max(weather.WindKph, weather.GustKph * 0.6) / 60, 0, 1)),
            Math.Round(weather.WindDirection),
            thunder,
            WindowsLit(sunAltitude, clouds * (kind == Precipitation.None ? 0.3 : 0.5)));
    }

    private static SkyView SkyFor(double altitude, bool rising, WeatherReading? weather, Effects effects, SkyPosition moon, MoonLight moonLight)
    {
        var (top, middle, horizon) = ClearAt(altitude);

        // A warmer horizon at dusk than at dawn.
        if (!rising)
        {
            var glow = Math.Exp(-Math.Pow((altitude - 0.5) / 5, 2)) * 0.3;
            horizon = Colour.Mix(horizon, EveningGlow, glow);
        }

        var daylight = SmoothStep(-12, 6, altitude);
        var overcastTop = Colour.Mix(OvercastNight.Top, OvercastDay.Top, daylight);
        var overcastMiddle = Colour.Mix(OvercastNight.Middle, OvercastDay.Middle, daylight);
        var overcastHorizon = Colour.Mix(OvercastNight.Horizon, OvercastDay.Horizon, daylight);

        var wet = effects.Precipitation != "none";
        var grey = Math.Clamp(Math.Pow(effects.Clouds, 1.4) * 0.8 + (wet ? 0.15 : 0) + (effects.Lightning ? 0.2 : 0), 0, 0.95);
        top = Colour.Mix(top, overcastTop, grey);
        middle = Colour.Mix(middle, overcastMiddle, grey);
        horizon = Colour.Mix(horizon, overcastHorizon, grey);

        if (effects.Lightning)
        {
            top = Colour.Mix(top, StormGrey, 0.35);
            middle = Colour.Mix(middle, StormGrey, 0.3);
        }

        // Fog washes the horizon out toward the middle of the sky.
        horizon = Colour.Mix(horizon, middle, effects.Fog * 0.5);

        var light = 0.18 + 0.82 * SmoothStep(-14, 8, altitude);
        if (altitude < -6 && moon.Altitude > 0) light += 0.08 * moonLight.Illumination;
        light *= 1 - 0.3 * grey;

        var stars = (1 - SmoothStep(-18, -6, altitude)) * Math.Pow(1 - (weather?.CloudCover ?? 0) / 100, 1.5) * (1 - effects.Fog);

        return new SkyView(
            top.ToHex(),
            middle.ToHex(),
            horizon.ToHex(),
            Colour.Mix(middle, horizon, 0.5).ToHex(),
            Round(Math.Clamp(light, 0.14, 1)),
            Round(Math.Clamp(stars, 0, 1)));
    }

    private static (Colour Top, Colour Middle, Colour Horizon) ClearAt(double altitude)
    {
        for (var i = 1; i < ClearSky.Length; i++)
        {
            var (hi, top, middle, horizon) = ClearSky[i];
            if (altitude > hi && i < ClearSky.Length - 1) continue;
            var (lo, topLo, middleLo, horizonLo) = ClearSky[i - 1];
            var t = Math.Clamp((altitude - lo) / (hi - lo), 0, 1);
            return (Colour.Mix(topLo, top, t), Colour.Mix(middleLo, middle, t), Colour.Mix(horizonLo, horizon, t));
        }
        return (ClearSky[^1].Top, ClearSky[^1].Middle, ClearSky[^1].Horizon);
    }

    private static double WindowsLit(double sunAltitude, double gloom) =>
        Round(Math.Max(1 - SmoothStep(-4, 6, sunAltitude), gloom));

    internal static string Summarise(int code, double altitude, bool rising, DateTimeOffset local, bool alwaysUp, bool alwaysDown)
    {
        var hour = local.Hour;
        string when;
        if (alwaysUp && (hour >= 21 || hour < 4)) when = "under the midnight sun";
        else if (alwaysDown && hour is >= 9 and < 16) when = "in the polar night";
        else if (altitude >= 6) when = hour switch { < 11 => "this morning", < 14 => "around midday", < 18 => "this afternoon", _ => "this evening" };
        else if (altitude >= Astronomy.HorizonAltitude) when = rising ? "at sunrise" : "at sunset";
        else if (altitude >= -6) when = rising ? "at dawn" : "at dusk";
        else if (hour is >= 1 and < 4) when = "in the small hours";
        else if (rising && hour is >= 4 and < 9) when = "before dawn";
        else when = "tonight";

        return $"{Wmo.Describe(code)} {when}";
    }

    internal static string MoonName(double phase) => phase switch
    {
        < 0.03 => "New moon",
        < 0.22 => "Waxing crescent",
        < 0.28 => "First quarter",
        < 0.47 => "Waxing gibbous",
        < 0.53 => "Full moon",
        < 0.72 => "Waning gibbous",
        < 0.78 => "Last quarter",
        < 0.97 => "Waning crescent",
        _ => "New moon",
    };

    private static string Clock(DateTimeOffset time, City city) =>
        TimeZoneInfo.ConvertTime(time, city.Zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Round(double value) => Math.Round(value, 3);
}
