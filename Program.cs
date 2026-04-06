using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StVrainToICSFunctionApp;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Helpers;
using System.Net.Http.Headers;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddControllers(controllers =>
        {
            controllers.OutputFormatters.Add(new ICSTextOutputFormatter());
        });

        services.AddHttpLogging(o => { });

        services.AddTransient<LinqNutritionUrlHandler>();
        services.AddHttpClient("LINQ", configureHttpClient =>
        {
            string apiEndpoint = Helpers.GetEnvironmentVariable<string>("APIEndpoint") ?? "https://api.linqconnect.com";
            configureHttpClient.BaseAddress = new Uri(apiEndpoint);
            ConfigureLinqConnectHeaders(configureHttpClient);
        })
        .AddHttpMessageHandler<LinqNutritionUrlHandler>()
        .AddStandardResilienceHandler();
    })
    .ConfigureLogging(logging =>
         {
             logging.Services.Configure<LoggerFilterOptions>(options =>
             {
                 LoggerFilterRule defaultRule = options.Rules.FirstOrDefault(rule => rule.ProviderName
                     == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
                 if (defaultRule is not null)
                 {
                     options.Rules.Remove(defaultRule);
                 }
             });
         })
    .Build();


host.Run();

static void ConfigureLinqConnectHeaders(HttpClient client)
{
    // Linq API expects browser-like CORS headers; linq-nutrition-url is a per-request UUID v4 (see main bundle addCustomHeaders + uuid).
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
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36");
}