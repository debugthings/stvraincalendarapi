using Microsoft.Extensions.Options;
using StVrainToICSFunctionApp.Options;

namespace StVrainToICSFunctionApp.Middleware;

public sealed class CalendarProxyMiddleware
{
    private readonly RequestDelegate _next;

    public CalendarProxyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        IOptions<ProxyOptions> proxyOptions,
        ILogger<CalendarProxyMiddleware> logger)
    {
        if (!IsMenuIcsPath(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        ProxyOptions options = proxyOptions.Value;
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
            HttpClient client = httpClientFactory.CreateClient("Proxy");
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
            logger.LogError(ex, "Failed to proxy {Path} to {Upstream}", context.Request.Path, target);
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync("Upstream calendar origin is unreachable.").ConfigureAwait(false);
        }
    }

    internal static bool IsMenuIcsPath(PathString path) =>
        path.HasValue
        && path.Value!.EndsWith("menu.ics", StringComparison.OrdinalIgnoreCase);
}
