using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kirie.Api;
using Kirie.Api.Scenes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Kirie.Tests;

/// <summary>The whole app, with its own database and synthetic weather so tests never touch the network.</summary>
public sealed class Book : WebApplicationFactory<Program>
{
    private readonly string _database = Path.Combine(Path.GetTempPath(), $"kirie-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Kirie", $"Data Source={_database}");
        builder.UseSetting("Sky:Offline", "true");
        builder.UseSetting("Kirie:CranesPerMinute", "3");
    }

    public HubConnection Reader() =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "hubs/book"), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
            })
            .Build();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_database); } catch (IOException) { }
    }
}

public class ApiTests(Book book) : IClassFixture<Book>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<T> Get<T>(string url) =>
        (await book.CreateClient().GetFromJsonAsync<T>(url, Json))!;

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 50; i++)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail("condition never became true");
    }

    [Fact]
    public async Task The_contents_list_every_place_with_weather()
    {
        BookView? contents = null;
        await Eventually(async () => (contents = await Get<BookView>("/api/book")).Weather == "synthetic");

        Assert.Equal(36, contents!.Cities.Length);
        Assert.InRange(contents.Subsolar.Latitude, -23.5, 23.5);
        Assert.All(contents.Cities, c => Assert.Matches(@"^\d\d:\d\d$", c.Clock));
    }

    [Fact]
    public async Task A_page_has_a_scene_and_a_missing_page_is_404()
    {
        var scene = await Get<Scene>("/api/cities/reykjavik/scene");
        Assert.Equal("Reykjavík", scene.City.Name);
        Assert.Matches("^#[0-9a-f]{6}$", scene.Sky.Top);
        Assert.InRange(scene.Sky.Light, 0, 1);

        var missing = await book.CreateClient().GetAsync("/api/cities/atlantis/scene");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Folding_a_crane_needs_a_real_colour_and_is_rate_limited()
    {
        // Its own app, so the rate limit it exhausts isn't shared with other tests.
        using var own = new Book();
        var client = own.CreateClient();
        var wrong = await client.PostAsJsonAsync("/api/cities/kathmandu/cranes", new { colour = "plaid" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var folded = await client.PostAsJsonAsync("/api/cities/kathmandu/cranes", new { colour = "red" });
        Assert.Equal(HttpStatusCode.Created, folded.StatusCode);
        var body = await folded.Content.ReadFromJsonAsync<FoldedView>(Json);
        Assert.Equal("red", body!.Crane.Colour);

        var scene = await client.GetFromJsonAsync<Scene>("/api/cities/kathmandu/scene", Json);
        Assert.Contains(scene!.Cranes, c => c.Id == body.Crane.Id);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/cities/kathmandu/cranes", new { colour = "blue" })).StatusCode);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Readers_hear_who_else_is_reading_and_cranes_folded_for_their_page()
    {
        await using var first = book.Reader();
        await using var second = book.Reader();

        var readers = new List<(string Page, int OnPage)>();
        var folded = new TaskCompletionSource<(string City, CraneView Crane, int Total)>();
        first.On<string, int, int>("Readers", (page, onPage, _) => { lock (readers) readers.Add((page, onPage)); });
        first.On<string, CraneView, int>("CraneFolded", (city, crane, total) => folded.TrySetResult((city, crane, total)));

        await first.StartAsync();
        await second.StartAsync();
        await first.InvokeAsync("Open", "tokyo");
        await second.InvokeAsync("Open", "tokyo");

        await Eventually(() => { lock (readers) return Task.FromResult(readers.Any(r => r is { Page: "tokyo", OnPage: 2 })); });

        await second.InvokeAsync("Open", "seoul");
        await Eventually(() => { lock (readers) return Task.FromResult(readers.Last(r => r.Page == "tokyo").OnPage == 1); });

        var response = await book.CreateClient().PostAsJsonAsync("/api/cities/tokyo/cranes", new { colour = "teal" });
        response.EnsureSuccessStatusCode();
        var (city, crane, total) = await folded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("tokyo", city);
        Assert.Equal("teal", crane.Colour);
        Assert.True(total >= 1);

        await Assert.ThrowsAsync<HubException>(() => first.InvokeAsync("Open", "atlantis"));
    }
}
