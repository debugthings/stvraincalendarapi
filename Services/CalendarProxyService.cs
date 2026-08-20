using Microsoft.Extensions.Options;
using StVrainToICSFunctionApp.Middleware;
using StVrainToICSFunctionApp.Options;

namespace StVrainToICSFunctionApp.Services;

public sealed class CalendarProxyService : ICalendarProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ProxyOptions> _proxyOptions;
    private readonly ILogger<CalendarProxyService> _logger;

    public CalendarProxyService(
        IHttpClientFactory httpClientFactory,
        IOptions<ProxyOptions> proxyOptions,
        ILogger<CalendarProxyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _proxyOptions = proxyOptions;
        _logger = logger;
    }

    public async Task ProxyMenuRequestAsync(HttpContext context)
    {
        ProxyOptions options = _proxyOptions.Value;
        if (string.IsNullOrWhiteSpace(options.UpstreamBaseUrl))
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync("Proxy upstream is not configured.").ConfigureAwait(false);
            return;
        }

        Uri baseUri = new(options.UpstreamBaseUrl.TrimEnd('/') + "/");
        string relative = context.Request.Path + context.Request.QueryString;
        Uri target = new(baseUri, relative.TrimStart('/'));

        try
        {
            HttpClient client = _httpClientFactory.CreateClient("Proxy");
            using HttpRequestMessage upstreamRequest = new(HttpMethod.Get, target);
            using HttpResponseMessage upstreamResponse = await client
                .SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted)
                .ConfigureAwait(false);

            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            string? contentType = upstreamResponse.Content.Headers.ContentType?.ToString();
            if (!string.IsNullOrEmpty(contentType))
            {
                context.Response.ContentType = contentType;
            }

            await upstreamResponse.Content.CopyToAsync(context.Response.Body, context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to proxy {Path} to {Upstream}", context.Request.Path, target);
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync("Upstream calendar origin is unreachable.").ConfigureAwait(false);
        }
    }
}
