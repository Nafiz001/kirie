using Kirie.Api.Cities;

namespace Kirie.Api.Scenes;

/// <summary>Everything the client needs to cut and light one page.</summary>
public sealed record Scene(
    CityView City,
    string LocalTime,
    string Clock,
    string Phase,
    bool Rising,
    string Summary,
    Conditions Conditions,
    SunView Sun,
    MoonView Moon,
    SkyView Sky,
    Effects Effects,
    DateTimeOffset? ObservedAt,
    string Source,
    CraneView[] Cranes,
    int CraneCount);

public sealed record CityView(
    string Id, string Name, string Country, double Latitude, double Longitude, string TimeZone, string[] Terrain, string Skyline)
{
    public static CityView From(City c) => new(c.Id, c.Name, c.Country, c.Latitude, c.Longitude, c.TimeZone, c.Terrain, c.Skyline);
}

public sealed record Conditions(
    int Code,
    string Description,
    double? TemperatureC,
    double? Humidity,
    double? CloudCover,
    double? PrecipitationMm,
    double? WindKph,
    double? WindDirection,
    double? VisibilityM);

/// <summary>Sunrise and sunset are local "HH:mm"; null when the sun doesn't rise or set that day.</summary>
public sealed record SunView(double Altitude, double Azimuth, string? Sunrise, string? Sunset, bool AlwaysUp, bool AlwaysDown);

public sealed record MoonView(double Altitude, double Azimuth, double Illumination, double Phase, string Name);

/// <summary>
/// The colours of the sky card, top to horizon, the colour the rest of the
/// paper is lit by, how bright that light is (0 to 1), and how many stars
/// show through (0 to 1).
/// </summary>
public sealed record SkyView(string Top, string Middle, string Horizon, string Ambient, double Light, double Stars);

/// <summary>What moves on the page. Every amount is 0 to 1.</summary>
public sealed record Effects(
    double Clouds,
    double CloudDarkness,
    string Precipitation,
    double Intensity,
    double Fog,
    double Wind,
    double WindDirection,
    bool Lightning,
    double WindowsLit);

public sealed record CraneView(long Id, string Colour, DateTimeOffset FoldedAt);

/// <summary>A line in the contents: enough to draw a city on the world-clock wheel.</summary>
public sealed record CityCard(
    string Id,
    string Name,
    string Country,
    double Latitude,
    double Longitude,
    string Clock,
    string Phase,
    string Summary,
    double? TemperatureC,
    int Code);
