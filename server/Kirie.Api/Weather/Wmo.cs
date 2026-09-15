namespace Kirie.Api.Weather;

public enum Precipitation
{
    None,
    Drizzle,
    Rain,
    Sleet,
    Snow,
    Hail,
}

/// <summary>
/// WMO weather interpretation codes (WMO 4677, as used by Open-Meteo): what each
/// one looks like, and what it's called.
/// </summary>
public static class Wmo
{
    public static bool IsKnown(int code) => Describe(code) != "Unknown";

    public static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mostly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Freezing fog",
        51 => "Light drizzle",
        53 => "Drizzle",
        55 => "Heavy drizzle",
        56 or 57 => "Freezing drizzle",
        61 => "Light rain",
        63 => "Rain",
        65 => "Heavy rain",
        66 or 67 => "Freezing rain",
        71 => "Light snow",
        73 => "Snow",
        75 => "Heavy snow",
        77 => "Snow grains",
        80 => "Light showers",
        81 => "Showers",
        82 => "Violent showers",
        85 => "Snow showers",
        86 => "Heavy snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Unknown",
    };

    public static Precipitation Kind(int code) => code switch
    {
        51 or 53 or 55 => Precipitation.Drizzle,
        56 or 57 or 66 or 67 => Precipitation.Sleet,
        61 or 63 or 65 or 80 or 81 or 82 or 95 => Precipitation.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => Precipitation.Snow,
        96 or 99 => Precipitation.Hail,
        _ => Precipitation.None,
    };

    /// <summary>0 to 1, from the code alone, for when the measured amount is missing or zero.</summary>
    public static double NominalIntensity(int code) => code switch
    {
        51 or 61 or 71 or 80 or 85 or 56 or 66 => 0.3,
        53 or 63 or 73 or 81 or 77 => 0.55,
        55 or 65 or 75 or 82 or 86 or 57 or 67 => 0.85,
        95 or 96 or 99 => 0.9,
        _ => 0,
    };

    public static bool IsThunder(int code) => code is 95 or 96 or 99;

    public static bool IsFog(int code) => code is 45 or 48;
}
