using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using StVrainToICSFunctionApp;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Middleware;
using StVrainToICSFunctionApp.Options;
using StVrainToICSFunctionApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.SectionName));
builder.Services.Configure<ProxyOptions>(builder.Configuration.GetSection(ProxyOptions.SectionName));

var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry()
        .WithLogging(_ => { }, static o => o.IncludeFormattedMessage = true)
        .UseAzureMonitorExporter();
}

bool proxyEnabled = builder.Configuration.GetValue<bool>("Proxy:Enabled");

if (proxyEnabled)
{
    builder.Services.AddHttpClient("Proxy", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
    });
}
else
{
    builder.Services.AddControllers(controllers =>
    {
        controllers.OutputFormatters.Add(new ICSTextOutputFormatter());
    });

    builder.Services.AddHttpLogging(o =>
    {
        o.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponseStatusCode;
    });

    builder.Services.AddLinqConnectHttpClient();
    builder.Services.AddSingleton<ILinqMenuClient, LinqMenuClient>();

    string dbPath = builder.Configuration.GetValue<string>("Cache:DatabasePath") ?? "data/menu-cache.db";
    if (!Path.IsPathRooted(dbPath))
    {
        dbPath = Path.Combine(builder.Environment.ContentRootPath, dbPath);
    }

    string? dbDir = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dbDir))
    {
        Directory.CreateDirectory(dbDir);
    }

    builder.Services.AddDbContext<MenuCacheDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
    builder.Services.AddScoped<IMenuCacheService, MenuCacheService>();
}

var app = builder.Build();

if (proxyEnabled)
{
    app.UseMiddleware<CalendarProxyMiddleware>();
}
else
{
    using (IServiceScope scope = app.Services.CreateScope())
    {
        MenuCacheDbContext db = scope.ServiceProvider.GetRequiredService<MenuCacheDbContext>();
        db.Database.EnsureCreated();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseHttpLogging();
    }

    app.MapControllers();
}

app.MapGet("/healthz", () => Results.Text(proxyEnabled ? "Proxy" : "Healthy", "text/plain"));
app.MapGet("/", () => Results.Text(
    """
    St. Vrain lunch menu calendar
    GET /Lunchmenu.ics
    GET /Breakfastmenu.ics
    GET /Academicmenu.ics
    GET /healthz
    """,
    "text/plain"));

app.Run();

public partial class Program;
