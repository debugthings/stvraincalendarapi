using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using AppHelpers = StVrainToICSFunctionApp.Helpers.Helpers;

namespace StVrainToICSFunctionApp;

/// <summary>
/// Registers the LINQ API <see cref="HttpClient"/> (same pipeline as the web app). Used by <see cref="Program"/> and E2E tests.
/// </summary>
/// <remarks>
/// Many edge WAFs block requests when HTTP headers mimic a browser but the TLS fingerprint is still .NET (not Chrome).
/// If 403/503 persist, try env <c>LinqMinimalBrowserHeaders=true</c> (fewer client hints) or ask LINQ for an allowlisted server-to-server path.
/// </remarks>
public static class LinqConnectServiceCollectionExtensions
{
    public static IServiceCollection AddLinqConnectHttpClient(this IServiceCollection services)
    {
        services.AddTransient<LinqNutritionUrlHandler>();
        services.AddHttpClient("LINQ", configureHttpClient =>
        {
            string apiEndpoint = AppHelpers.GetEnvironmentVariable<string>("APIEndpoint") ?? "https://api.linqconnect.com";
            configureHttpClient.BaseAddress = new Uri(apiEndpoint);

            // Some WAFs behave differently on HTTP/2 vs HTTP/1.1. Default to HTTP/1.1 unless LinqUseHttp2=true.
            if (EnvFlag("LinqUseHttp2"))
            {
                configureHttpClient.DefaultRequestVersion = HttpVersion.Version20;
                configureHttpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            }
            else
            {
                configureHttpClient.DefaultRequestVersion = HttpVersion.Version11;
                configureHttpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            }

            ConfigureLinqConnectHeaders(configureHttpClient);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
        })
        .AddHttpMessageHandler<LinqNutritionUrlHandler>()
        .AddStandardResilienceHandler();

        return services;
    }

    internal static void ConfigureLinqConnectHeaders(HttpClient client)
    {
        string? refererOverride = AppHelpers.GetEnvironmentVariable<string>("LinqApiReferer");
        string referer = string.IsNullOrWhiteSpace(refererOverride)
            ? BuildDefaultLinqPublicMenuReferer()
            : refererOverride.Trim();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", referer);

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.AcceptLanguage.Clear();

        // Fewer “browser” hints can reduce bot scores when TLS still looks like .NET, not Chrome.
        if (EnvFlag("LinqMinimalBrowserHeaders"))
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
            return;
        }

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.9));

        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://linqconnect.com");
        client.DefaultRequestHeaders.TryAddWithoutValidation("priority", "u=1, i");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"146\", \"Not-A.Brand\";v=\"24\", \"Google Chrome\";v=\"146\"");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-dest", "empty");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-mode", "cors");
        client.DefaultRequestHeaders.TryAddWithoutValidation("sec-fetch-site", "same-site");
    }

    internal static string BuildDefaultLinqPublicMenuReferer()
    {
        string menuCode = AppHelpers.GetEnvironmentVariable<string>("LinqDistrictMenuCode") ?? "DCN3CB";
        string buildingId = AppHelpers.GetEnvironmentVariable<string>("LinqPublicMenuBuildingId") ?? "67673211-c4be-ed11-82b1-880d996bcdd8";
        return $"https://linqconnect.com/public/menu/{menuCode}?buildingId={buildingId}";
    }

    private static bool EnvFlag(string name)
    {
        string? v = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "1", StringComparison.Ordinal);
    }
}
