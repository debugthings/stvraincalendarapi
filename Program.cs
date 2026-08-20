using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Azure.Functions.Worker.Builder;
using StVrainToICSFunctionApp;
using StVrainToICSFunctionApp.Middleware;
using StVrainToICSFunctionApp.Options;
using StVrainToICSFunctionApp.Services;

bool functionsHost = IsAzureFunctionsHost();
var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

if (functionsHost)
{
    var builder = FunctionsApplication.CreateBuilder(args);
    builder.ConfigureFunctionsWebApplication();
    builder.Logging.AddConsole();
    ConfigureShared(builder.Services, builder.Configuration);
    RegisterMode(builder.Services, builder.Configuration, builder.Environment.ContentRootPath, functionsHost: true);

    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        builder.Services.AddOpenTelemetry()
            .WithLogging(_ => { }, static o => o.IncludeFormattedMessage = true)
            .UseAzureMonitorExporter();
    }

    var host = builder.Build();
    if (!LunchMenuHostExtensions.IsProxyEnabled(host.Services.GetRequiredService<IConfiguration>()))
    {
        host.Services.EnsureMenuCacheCreated();
    }

    host.Run();
}
else
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.AddConsole();
    ConfigureShared(builder.Services, builder.Configuration);
    RegisterMode(builder.Services, builder.Configuration, builder.Environment.ContentRootPath, functionsHost: false);

    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        builder.Services.AddOpenTelemetry()
            .WithLogging(_ => { }, static o => o.IncludeFormattedMessage = true)
            .UseAzureMonitorExporter();
    }

    var app = builder.Build();
    bool proxyEnabled = LunchMenuHostExtensions.IsProxyEnabled(app.Configuration);

    if (proxyEnabled)
    {
        app.UseMiddleware<CalendarProxyMiddleware>();
    }
    else
    {
        app.Services.EnsureMenuCacheCreated();
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpLogging();
        }

        app.MapControllers();
    }

    app.MapGet("/healthz", () => Results.Text(LunchMenuHostExtensions.HealthText(app.Configuration), "text/plain"));
    app.MapGet("/", () => Results.Text(
        $$"""
        St. Vrain lunch menu calendar ({{LunchMenuHostExtensions.ModeDescription(app.Configuration)}})
        GET /Lunchmenu.ics
        GET /Breakfastmenu.ics
        GET /Academicmenu.ics
        GET /rhe/lunchmenu
        GET /ems/lunchmenu
        GET /healthz
        """,
        "text/plain"));
    app.Run();
}

static void ConfigureShared(IServiceCollection services, IConfiguration configuration)
{
    services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
    services.Configure<ProxyOptions>(configuration.GetSection(ProxyOptions.SectionName));
    services.AddSingleton<SchoolShortcutCatalog>();
}

static void RegisterMode(IServiceCollection services, IConfiguration configuration, string contentRootPath, bool functionsHost)
{
    if (LunchMenuHostExtensions.IsProxyEnabled(configuration))
    {
        services.AddLunchMenuProxy();
        if (!functionsHost)
        {
            // Kestrel proxy uses middleware; Functions uses ConvertToICS + ProxyMenuCalendarService.
        }
    }
    else
    {
        if (!functionsHost)
        {
            services.AddHttpLogging(o =>
            {
                o.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponseStatusCode;
            });
        }

        services.AddLunchMenuOriginServices(configuration, contentRootPath);
    }
}

static bool IsAzureFunctionsHost()
{
    string? runtime = Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME");
    return !string.IsNullOrWhiteSpace(runtime);
}

public partial class Program;
