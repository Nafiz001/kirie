using System.Collections.Concurrent;
using Kirie.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Kirie.Api.Weather;

/// <summary>
/// The latest reading for every city, held in memory for fast scene requests
/// and written through to the database so a restart doesn't begin with an
/// empty sky.
/// </summary>
public sealed class WeatherStore(IServiceScopeFactory scopes)
{
    private readonly ConcurrentDictionary<string, WeatherReading> _latest = new();

    public WeatherReading? For(string cityId) => _latest.GetValueOrDefault(cityId);

    public bool IsEmpty => _latest.IsEmpty;

    public async Task LoadAsync(CancellationToken cancellation)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KirieDb>();
        foreach (var reading in await db.Readings.AsNoTracking().ToListAsync(cancellation))
            _latest[reading.CityId] = reading;
    }

    /// <summary>Saves the readings and returns the ids of cities whose sky visibly changed.</summary>
    public async Task<IReadOnlyList<string>> SaveAsync(IReadOnlyList<WeatherReading> readings, CancellationToken cancellation)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KirieDb>();
        var existing = await db.Readings.ToDictionaryAsync(r => r.CityId, cancellation);
        var changed = new List<string>();

        foreach (var reading in readings)
        {
            if (reading.LooksDifferentFrom(_latest.GetValueOrDefault(reading.CityId)))
                changed.Add(reading.CityId);

            if (existing.TryGetValue(reading.CityId, out var row))
                db.Entry(row).CurrentValues.SetValues(reading);
            else
                db.Readings.Add(reading);
        }

        await db.SaveChangesAsync(cancellation);
        foreach (var reading in readings)
            _latest[reading.CityId] = reading;
        return changed;
    }
}
