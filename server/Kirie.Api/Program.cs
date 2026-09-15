using System.Threading.RateLimiting;
using Kirie.Api;
using Kirie.Api.Cities;
using Kirie.Api.Cranes;
using Kirie.Api.Data;
using Kirie.Api.Live;
using Kirie.Api.Scenes;
using Kirie.Api.Weather;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<SkyOptions>(config.GetSection("Sky"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<Atlas>();
builder.Services.AddSingleton<SceneComposer>();
builder.Services.AddSingleton<WeatherStore>();
builder.Services.AddSingleton<Readers>();
builder.Services.AddSingleton<SyntheticSource>();
builder.Services.AddDbContext<KirieDb>(db => db.UseSqlite(config.GetConnectionString("Kirie")));
builder.Services.AddScoped<CraneBook>();

if (config.GetValue<bool>("Sky:Offline"))
{
    builder.Services.AddSingleton<IWeatherSource>(services => services.GetRequiredService<SyntheticSource>());
}
else
{
    builder.Services
        .AddHttpClient<IWeatherSource, OpenMeteoSource>(http =>
        {
            http.BaseAddress = new Uri("https://api.open-meteo.com/");
            http.DefaultRequestHeaders.UserAgent.ParseAdd("kirie/1.0");
        })
        .AddStandardResilienceHandler();
}

builder.Services.AddHostedService<SkyKeeper>();
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddRateLimiter(limits =>
{
    limits.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limits.AddPolicy(BookEndpoints.CranePolicy, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("Kirie:CranesPerMinute", 6),
            Window = TimeSpan.FromMinutes(1),
        }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<KirieDb>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseRateLimiter();

// In production the server can also host the built book (web/build), so the
// whole thing runs as one process on one origin.
var web = config["Kirie:Web"];
if (!string.IsNullOrEmpty(web) && Directory.Exists(web))
{
    var files = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetFullPath(web));
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
}

app.MapHealthChecks("/health");
app.MapBook();
app.MapHub<BookHub>("/hubs/book");

app.Run();

public partial class Program;
