using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;

namespace StVrainToICSFunctionApp;

// Per request: uuid v4 as linq-nutrition-url, single raw User-Agent (see SendAsync), then log outbound headers.
internal sealed class LinqNutritionUrlHandler : DelegatingHandler
{
    private const string DefaultBrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36";

    private readonly ILogger<LinqNutritionUrlHandler> _logger;

    public LinqNutritionUrlHandler(ILogger<LinqNutritionUrlHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Clear typed + string-based UA; set one string (runtime may split into product tokens — that still serializes as a single User-Agent header line).
        request.Headers.Remove("User-Agent");
        request.Headers.UserAgent.Clear();
        string? uaOverride = Environment.GetEnvironmentVariable("LinqUserAgent", EnvironmentVariableTarget.Process);
        string ua = string.IsNullOrWhiteSpace(uaOverride) ? DefaultBrowserUserAgent : uaOverride.Trim();
        request.Headers.TryAddWithoutValidation("User-Agent", ua);

        string? pinned = Environment.GetEnvironmentVariable("LinqNutritionUrl", EnvironmentVariableTarget.Process);
        string value = string.IsNullOrWhiteSpace(pinned)
            ? Guid.NewGuid().ToString("D").ToLowerInvariant()
            : pinned.Trim();

        request.Headers.TryAddWithoutValidation("linq-nutrition-url", value);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            // Property name avoids generic "Headers" (some pipelines treat it as sensitive); full text also appears on message when OTel IncludeFormattedMessage is true.
            _logger.LogInformation(
                "LINQ HTTP {Method} {RequestUri} outbound headers: {LinqOutboundHeaders}",
                request.Method,
                request.RequestUri,
                FormatHeaders(request));
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "LINQ HTTP response {StatusCode} {ReasonPhrase} for {RequestUri}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                request.RequestUri);
        }

        return response;
    }

    private static string FormatHeaders(HttpRequestMessage request)
    {
        var sb = new StringBuilder(512);
        foreach (var pair in request.Headers)
        {
            // User-Agent is stored as multiple product-token strings; on the wire it is still one header line (space-separated). Logging each token looked like duplicate User-Agent headers.
            if (pair.Key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("User-Agent: ").Append(string.Join(" ", pair.Value)).Append("; ");
                continue;
            }

            foreach (var v in pair.Value)
                sb.Append(pair.Key).Append(": ").Append(v).Append("; ");
        }

        if (request.Content is not null)
        {
            foreach (var pair in request.Content.Headers)
            {
                foreach (var v in pair.Value)
                    sb.Append(pair.Key).Append(": ").Append(v).Append("; ");
            }
        }

        return sb.Length == 0 ? "(none)" : sb.ToString();
    }
}
