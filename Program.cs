using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    .Build();
host.Run();