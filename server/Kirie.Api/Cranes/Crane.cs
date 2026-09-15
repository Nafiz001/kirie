using System.Security.Cryptography;
using System.Text;
using Kirie.Api.Data;
using Kirie.Api.Scenes;
using Microsoft.EntityFrameworkCore;

namespace Kirie.Api.Cranes;

/// <summary>A paper crane someone folded for a city. It flies on that page for a day.</summary>
public sealed class Crane
{
    public long Id { get; set; }
    public required string CityId { get; set; }
    public required string Colour { get; set; }
    public DateTimeOffset FoldedAt { get; set; }

    /// <summary>A salted hash of who folded it, never the address itself.</summary>
    public required string Folder { get; set; }
}

public static class CranePaper
{
    /// <summary>The origami papers on offer.</summary>
    public static readonly IReadOnlyList<string> Colours = ["red", "orange", "yellow", "green", "teal", "blue", "violet", "white"];

    public static readonly TimeSpan FliesFor = TimeSpan.FromHours(24);
    public const int MostOnAPage = 24;
}

public sealed class CraneBook(KirieDb db, TimeProvider clock)
{
    public async Task<(CraneView[] Flying, int Total)> ForCityAsync(string cityId, CancellationToken cancellation)
    {
        var since = clock.GetUtcNow() - CranePaper.FliesFor;
        var flying = await db.Cranes.AsNoTracking()
            .Where(c => c.CityId == cityId && c.FoldedAt > since)
            .OrderByDescending(c => c.FoldedAt)
            .Take(CranePaper.MostOnAPage)
            .Select(c => new CraneView(c.Id, c.Colour, c.FoldedAt))
            .ToArrayAsync(cancellation);
        var total = await db.Cranes.CountAsync(c => c.CityId == cityId, cancellation);
        return (flying, total);
    }

    public async Task<(CraneView Crane, int Total)> FoldAsync(string cityId, string colour, string folder, CancellationToken cancellation)
    {
        var crane = new Crane { CityId = cityId, Colour = colour, Folder = folder, FoldedAt = clock.GetUtcNow() };
        db.Cranes.Add(crane);
        await db.SaveChangesAsync(cancellation);
        var total = await db.Cranes.CountAsync(c => c.CityId == cityId, cancellation);
        return (new CraneView(crane.Id, crane.Colour, crane.FoldedAt), total);
    }

    public static string Fingerprint(string? address, string salt) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(salt), Encoding.UTF8.GetBytes(address ?? "unknown")))[..32];
}
