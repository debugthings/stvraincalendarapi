using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using AppHelpers = StVrainToICSFunctionApp.Helpers.Helpers;

namespace StVrainToICSFunctionApp;

/// <summary>
/// Registers the LINQ API <see cref="HttpClient"/> (same pipeline as the function app). Used by <see cref="Program"/> and E2E tests.
/// </summary>
public static class LinqConnectServiceCollectionExtensions
{
    public static IServiceCollection AddLinqConnectHttpClient(this IServiceCollection services)
    {
        services.AddTransient<LinqNutritionUrlHandler>();
        services.AddHttpClient("LINQ", configureHttpClient =>
        {
            string apiEndpoint = AppHelpers.GetEnvironmentVariable<string>("APIEndpoint") ?? "https://api.linqconnect.com";
            configureHttpClient.BaseAddress = new Uri(apiEndpoint);
            ConfigureLinqConnectHeaders(configureHttpClient);
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
}
