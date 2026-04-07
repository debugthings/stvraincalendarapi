using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StVrainToICSFunctionApp;
using StVrainToICSFunctionApp.Formatters;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// OpenTelemetry sends logs to Azure Monitor; the portal "Log stream" follows stdout — add Console so ILogger lines appear there too.
builder.Logging.AddConsole();

// UseAzureMonitorExporter requires APPLICATIONINSIGHTS_CONNECTION_STRING (set in local.settings.json / Azure). Without it, startup throws — unlike the legacy AI SDK.
var appInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
var openTelemetryBuilder = builder.Services.AddOpenTelemetry()
    .WithLogging(_ => { }, static o => o.IncludeFormattedMessage = true)
    .UseFunctionsWorkerDefaults();

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    openTelemetryBuilder.UseAzureMonitorExporter();
}

builder.Services.AddControllers(controllers =>
{
    controllers.OutputFormatters.Add(new ICSTextOutputFormatter());
});

builder.Services.AddHttpLogging(o => { });

builder.Services.AddLinqConnectHttpClient();

builder.Build().Run();
