namespace Kirie.Api.Weather;

/// <summary>The latest observed weather for one city. One row per city, replaced on every refresh.</summary>
public sealed class WeatherReading
{
    public required string CityId { get; set; }

    /// <summary>When the conditions were observed, which trails the fetch by up to the provider's interval.</summary>
    public DateTimeOffset ObservedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>"open-meteo" for real observations, "synthetic" when the provider couldn't be reached.</summary>
    public required string Source { get; set; }

    public int Code { get; set; }
    public double TemperatureC { get; set; }
    public double Humidity { get; set; }
    public double CloudCover { get; set; }
    public double PrecipitationMm { get; set; }
    public double SnowfallCm { get; set; }
    public double WindKph { get; set; }
    public double WindDirection { get; set; }
    public double GustKph { get; set; }
    public double VisibilityM { get; set; }

    /// <summary>
    /// True when the change from <paramref name="other"/> is one a reader would see in the paper sky.
    /// Temperature ticking by a tenth of a degree isn't.
    /// </summary>
    public bool LooksDifferentFrom(WeatherReading? other) =>
        other is null
        || other.Code != Code
        || Math.Abs(other.CloudCover - CloudCover) >= 10
        || Math.Abs(other.PrecipitationMm - PrecipitationMm) >= 0.2
        || Math.Abs(other.WindKph - WindKph) >= 5
        || Math.Abs(other.VisibilityM - VisibilityM) >= 1000
        || Math.Abs(other.TemperatureC - TemperatureC) >= 1;
}
