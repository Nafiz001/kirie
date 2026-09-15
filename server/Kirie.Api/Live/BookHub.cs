using System.Collections.Concurrent;
using Kirie.Api.Cities;
using Kirie.Api.Scenes;
using Microsoft.AspNetCore.SignalR;

namespace Kirie.Api.Live;

/// <summary>What the server tells readers.</summary>
public interface IReader
{
    /// <summary>The page they're reading has changed: new weather, or the sun has moved.</summary>
    Task Scene(Scene scene);

    /// <summary>How many people have a page open, and how many in the whole book.</summary>
    Task Readers(string page, int onPage, int inBook);

    Task CraneFolded(string cityId, CraneView crane, int total);
}

/// <summary>Who has which page open. A connection reads one page at a time.</summary>
public sealed class Readers
{
    private readonly ConcurrentDictionary<string, string> _pageByConnection = new();

    public string? Open(string connection, string page)
    {
        string? previous = null;
        _pageByConnection.AddOrUpdate(connection, page, (_, old) =>
        {
            previous = old;
            return page;
        });
        return previous == page ? null : previous;
    }

    public string? Close(string connection) => _pageByConnection.TryRemove(connection, out var page) ? page : null;

    public int On(string page) => _pageByConnection.Values.Count(p => p == page);

    public int Total => _pageByConnection.Count;

    public IReadOnlyCollection<string> OpenPages => _pageByConnection.Values.Distinct().ToArray();
}

/// <summary>
/// One connection per reader. Opening a page joins that page's group, so the
/// reader hears when its sky changes and when someone folds a crane for it.
/// "contents" is the world-clock page.
/// </summary>
public sealed class BookHub(Readers readers, Atlas atlas) : Hub<IReader>
{
    public const string Contents = "contents";

    public async Task Open(string page)
    {
        if (page != Contents && atlas.Find(page) is null)
            throw new HubException($"There is no page called '{page}'.");

        var previous = readers.Open(Context.ConnectionId, page);
        if (previous is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous);
            await Announce(previous);
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, page);
        await Announce(page);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (readers.Close(Context.ConnectionId) is { } page)
            await Announce(page);
        await base.OnDisconnectedAsync(exception);
    }

    private Task Announce(string page) => Clients.All.Readers(page, readers.On(page), readers.Total);
}
