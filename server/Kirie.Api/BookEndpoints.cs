using Kirie.Api.Cities;
using Kirie.Api.Cranes;
using Kirie.Api.Live;
using Kirie.Api.Scenes;
using Kirie.Api.Sky;
using Kirie.Api.Weather;
using Microsoft.AspNetCore.SignalR;

namespace Kirie.Api;

public sealed record BookView(DateTimeOffset Now, SubsolarView Subsolar, CityCard[] Cities, int Readers, string Weather);

public sealed record SubsolarView(double Latitude, double Longitude);

public sealed record FoldRequest(string? Colour);

public sealed record FoldedView(CraneView Crane, int Total);

public static class BookEndpoints
{
    public const string CranePolicy = "cranes";

    public static void MapBook(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // The contents page: every city, where the sun is overhead, who's reading.
        api.MapGet("/book", (Atlas atlas, WeatherStore store, SceneComposer composer, Readers readers, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();
            var (latitude, longitude) = Astronomy.Subsolar(now);
            var readings = atlas.All.Select(c => store.For(c.Id)).ToArray();
            var weather = readings.Where(r => r is not null).GroupBy(r => r!.Source).MaxBy(g => g.Count())?.Key ?? "none";
            return new BookView(
                now,
                new SubsolarView(Math.Round(latitude, 3), Math.Round(longitude, 3)),
                atlas.All.Select((c, i) => composer.Card(c, readings[i], now)).ToArray(),
                readers.Total,
                weather);
        });

        api.MapGet("/cities/{id}/scene", async (string id, Atlas atlas, WeatherStore store, SceneComposer composer, CraneBook cranes, TimeProvider clock, CancellationToken cancellation) =>
        {
            if (atlas.Find(id) is not { } city)
                return Results.Problem(statusCode: 404, title: $"There is no page called '{id}'.");
            var (flying, total) = await cranes.ForCityAsync(city.Id, cancellation);
            return Results.Ok(composer.Compose(city, store.For(city.Id), clock.GetUtcNow(), flying, total));
        });

        api.MapPost("/cities/{id}/cranes", async (
            string id,
            FoldRequest request,
            HttpContext http,
            Atlas atlas,
            CraneBook cranes,
            IConfiguration config,
            IHubContext<BookHub, IReader> hub,
            CancellationToken cancellation) =>
        {
            if (atlas.Find(id) is not { } city)
                return Results.Problem(statusCode: 404, title: $"There is no page called '{id}'.");
            if (request.Colour is null || !CranePaper.Colours.Contains(request.Colour))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["colour"] = [$"Choose one of: {string.Join(", ", CranePaper.Colours)}."],
                });

            var folder = CraneBook.Fingerprint(http.Connection.RemoteIpAddress?.ToString(), config["Kirie:Salt"] ?? "kirie");
            var (crane, total) = await cranes.FoldAsync(city.Id, request.Colour, folder, cancellation);
            await hub.Clients.Group(city.Id).CraneFolded(city.Id, crane, total);
            return Results.Created($"/api/cities/{city.Id}/cranes/{crane.Id}", new FoldedView(crane, total));
        }).RequireRateLimiting(CranePolicy);
    }
}
