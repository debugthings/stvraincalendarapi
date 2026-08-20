using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp.Middleware;

public sealed class CalendarProxyMiddleware
{
    private readonly RequestDelegate _next;

    public CalendarProxyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICalendarProxyService proxy)
    {
        if (!IsMenuIcsPath(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        await proxy.ProxyMenuRequestAsync(context).ConfigureAwait(false);
    }

    internal static bool IsMenuIcsPath(PathString path) =>
        path.HasValue
        && path.Value!.EndsWith("menu.ics", StringComparison.OrdinalIgnoreCase);
}
