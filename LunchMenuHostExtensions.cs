using Microsoft.Extensions.Options;
using StVrainToICSFunctionApp.Options;
using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp;

public static class LunchMenuHostExtensions
{
    public static bool IsProxyEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>("Proxy:Enabled");

    public static void AddLunchMenuProxy(this IServiceCollection services)
    {
        services.AddHttpClient("Proxy", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddSingleton<ICalendarProxyService, CalendarProxyService>();
        services.AddSingleton<IMenuCalendarService, ProxyMenuCalendarService>();
    }

    public static void AddLunchMenuOriginServices(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.AddLunchMenuOrigin(configuration, contentRootPath);
        services.AddScoped<IMenuCalendarService, OriginMenuCalendarService>();
    }

    public static string HealthText(IConfiguration configuration) =>
        IsProxyEnabled(configuration) ? "Proxy" : "Healthy";

    public static string ModeDescription(IConfiguration configuration) =>
        IsProxyEnabled(configuration)
            ? $"Proxy -> {configuration.GetValue<string>("Proxy:UpstreamBaseUrl") ?? "(not set)"}"
            : "Origin (LINQ + SQLite cache)";
}
