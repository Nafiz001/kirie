using Kirie.Api.Cities;
using Kirie.Api.Cranes;
using Kirie.Api.Scenes;
using Kirie.Api.Weather;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Kirie.Api.Live;

public sealed class SkyOptions
{
    /// <summary>How often to ask for new weather. Open-Meteo updates current conditions every 15 minutes.</summary>
    public TimeSpan WeatherEvery { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How often open pages are redrawn, because the sun and moon keep moving.</summary>
    public TimeSpan SkyEvery { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Skip the real provider entirely and use synthetic weather.</summary>
    public bool Offline { get; set; }
}

/// <summary>
/// Keeps the sky current. Fetches weather on a schedule, falling back to
/// synthetic readings when the provider can't be reached and nothing better is
/// stored, and pushes fresh scenes to whoever has an affected page open.
/// </summary>
public sealed class SkyKeeper(
    Atlas atlas,
    WeatherStore store,
    IWeatherSource source,
    SyntheticSource synthetic,
    Readers readers,
    IServiceScopeFactory scopes,
    IHubContext<BookHub, IReader> hub,
    TimeProvider clock,
    IOptions<SkyOptions> options,
    ILogger<SkyKeeper> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stopping)
    {
        await store.LoadAsync(stopping);
        await RefreshWeatherAsync(stopping);

        var weather = RunEvery(options.Value.WeatherEvery, RefreshWeatherAsync, stopping);
        var sky = RunEvery(options.Value.SkyEvery, token => RedrawAsync(readers.OpenPages, token), stopping);
        await Task.WhenAll(weather, sky);
    }

    public async Task RefreshWeatherAsync(CancellationToken cancellation)
    {
        IReadOnlyList<WeatherReading> readings;
        try
        {
            readings = await source.FetchAsync(atlas.All, clock.GetUtcNow(), cancellation);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log.LogWarning(error, "Couldn't fetch weather from {Source}", source.Name);
            if (!store.IsEmpty) return;
            log.LogWarning("No stored weather either; using synthetic weather until {Source} answers", source.Name);
            readings = await synthetic.FetchAsync(atlas.All, clock.GetUtcNow(), cancellation);
        }

        var changed = await store.SaveAsync(readings, cancellation);
        log.LogInformation("Weather for {Count} places from {Source}; {Changed} look different", readings.Count, readings.FirstOrDefault()?.Source, changed.Count);
        await RedrawAsync(changed.Intersect(readers.OpenPages).ToArray(), cancellation);
    }

    private async Task RedrawAsync(IReadOnlyCollection<string> pages, CancellationToken cancellation)
    {
        if (pages.Count == 0) return;
        using var scope = scopes.CreateScope();
        var cranes = scope.ServiceProvider.GetRequiredService<CraneBook>();
        var composer = scope.ServiceProvider.GetRequiredService<SceneComposer>();

        foreach (var page in pages)
        {
            if (atlas.Find(page) is not { } city) continue;
            var (flying, total) = await cranes.ForCityAsync(city.Id, cancellation);
            var scene = composer.Compose(city, store.For(city.Id), clock.GetUtcNow(), flying, total);
            await hub.Clients.Group(city.Id).Scene(scene);
        }
    }

    private async Task RunEvery(TimeSpan interval, Func<CancellationToken, Task> work, CancellationToken stopping)
    {
        using var timer = new PeriodicTimer(interval, clock);
        while (await timer.WaitForNextTickAsync(stopping))
        {
            try
            {
                await work(stopping);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                log.LogError(error, "Keeping the sky failed; trying again next time");
            }
        }
    }
}
