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

        var httpBuilder = services.AddHttpClient("LINQ", configureHttpClient =>
        {
            string apiEndpoint = Helpers.GetEnvironmentVariable<string>("APIEndpoint") ?? "https://api.linqconnect.com";
            configureHttpClient.BaseAddress = new Uri(apiEndpoint);
            configureHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            configureHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            configureHttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        })
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