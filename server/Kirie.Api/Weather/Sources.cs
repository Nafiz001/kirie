using System.Globalization;
using System.Text.Json.Serialization;
using Kirie.Api.Cities;

namespace Kirie.Api.Weather;

public interface IWeatherSource
{
    string Name { get; }
    Task<IReadOnlyList<WeatherReading>> FetchAsync(IReadOnlyList<City> cities, DateTimeOffset now, CancellationToken cancellation);
}

/// <summary>
/// Current conditions from Open-Meteo, which needs no key and answers for many
/// coordinates in a single request.
/// </summary>
public sealed class OpenMeteoSource(HttpClient http) : IWeatherSource
{
    private const string Fields =
        "temperature_2m,relative_humidity_2m,precipitation,snowfall,weather_code,cloud_cover," +
        "wind_speed_10m,wind_direction_10m,wind_gusts_10m,visibility";

    public string Name => "open-meteo";

    public async Task<IReadOnlyList<WeatherReading>> FetchAsync(IReadOnlyList<City> cities, DateTimeOffset now, CancellationToken cancellation)
    {
        var readings = new List<WeatherReading>(cities.Count);
        foreach (var batch in cities.Chunk(50))
        {
            var latitudes = string.Join(',', batch.Select(c => c.Latitude.ToString("0.####", CultureInfo.InvariantCulture)));
            var longitudes = string.Join(',', batch.Select(c => c.Longitude.ToString("0.####", CultureInfo.InvariantCulture)));
            var url = $"v1/forecast?latitude={latitudes}&longitude={longitudes}&current={Fields}&timezone=GMT&wind_speed_unit=kmh";

            // One coordinate comes back as an object, several as an array.
            Response[] body = batch.Length == 1
                ? [await http.GetFromJsonAsync<Response>(url, cancellation) ?? throw new InvalidDataException("Empty response")]
                : await http.GetFromJsonAsync<Response[]>(url, cancellation) ?? throw new InvalidDataException("Empty response");

            if (body.Length != batch.Length)
                throw new InvalidDataException($"Asked for {batch.Length} places, got {body.Length}");

            for (var i = 0; i < batch.Length; i++)
            {
                var c = body[i].Current ?? throw new InvalidDataException($"No current conditions for {batch[i].Id}");
                readings.Add(new WeatherReading
                {
                    CityId = batch[i].Id,
                    Source = Name,
                    FetchedAt = now,
                    ObservedAt = DateTime.SpecifyKind(DateTime.Parse(c.Time, CultureInfo.InvariantCulture), DateTimeKind.Utc),
                    Code = c.WeatherCode,
                    TemperatureC = c.Temperature,
                    Humidity = c.Humidity,
                    CloudCover = c.CloudCover,
                    PrecipitationMm = c.Precipitation,
                    SnowfallCm = c.Snowfall,
                    WindKph = c.WindSpeed,
                    WindDirection = c.WindDirection,
                    GustKph = c.WindGusts,
                    VisibilityM = c.Visibility ?? 24_000,
                });
            }
        }
        return readings;
    }

    private sealed record Response([property: JsonPropertyName("current")] Current? Current);

    private sealed record Current(
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("weather_code")] int WeatherCode,
        [property: JsonPropertyName("temperature_2m")] double Temperature,
        [property: JsonPropertyName("relative_humidity_2m")] double Humidity,
        [property: JsonPropertyName("cloud_cover")] double CloudCover,
        [property: JsonPropertyName("precipitation")] double Precipitation,
        [property: JsonPropertyName("snowfall")] double Snowfall,
        [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
        [property: JsonPropertyName("wind_direction_10m")] double WindDirection,
        [property: JsonPropertyName("wind_gusts_10m")] double WindGusts,
        [property: JsonPropertyName("visibility")] double? Visibility);
}

/// <summary>
/// Plausible, repeatable weather for when the real thing can't be reached, so
/// the book still opens offline. Readings are marked as synthetic and the
/// client says so.
/// </summary>
public sealed class SyntheticSource : IWeatherSource
{
    private static readonly int[] Codes = [0, 1, 2, 3, 3, 45, 51, 61, 63, 80, 95, 1, 2, 71, 73];

    public string Name => "synthetic";

    public Task<IReadOnlyList<WeatherReading>> FetchAsync(IReadOnlyList<City> cities, DateTimeOffset now, CancellationToken cancellation)
    {
        var hour = now.ToUnixTimeSeconds() / 3600;
        IReadOnlyList<WeatherReading> readings = cities.Select(city =>
        {
            var seed = Math.Abs(HashCode.Combine(city.Id.GetHashCode(StringComparison.Ordinal), hour / 3));
            var random = new Random(seed);
            var polar = Math.Abs(city.Latitude) / 90;
            var temperature = 29 - polar * 45 + random.NextDouble() * 6;
            var code = Codes[random.Next(Codes.Length)];
            if (Wmo.Kind(code) == Precipitation.Snow && temperature > 2) code = 61;
            if (Wmo.Kind(code) == Precipitation.Rain && temperature < -2) code = 71;
            var wet = Wmo.Kind(code) != Precipitation.None;
            return new WeatherReading
            {
                CityId = city.Id,
                Source = Name,
                FetchedAt = now,
                ObservedAt = now,
                Code = code,
                TemperatureC = Math.Round(temperature, 1),
                Humidity = wet ? 90 : 40 + random.Next(40),
                CloudCover = code switch { 0 => 0, 1 => 20, 2 => 50, _ => 90 },
                PrecipitationMm = wet ? Math.Round(Wmo.NominalIntensity(code) * 4, 1) : 0,
                WindKph = Math.Round(random.NextDouble() * 35, 1),
                WindDirection = random.Next(360),
                GustKph = Math.Round(random.NextDouble() * 55, 1),
                VisibilityM = Wmo.IsFog(code) ? 400 : 20_000,
            };
        }).ToList();
        return Task.FromResult(readings);
    }
}
