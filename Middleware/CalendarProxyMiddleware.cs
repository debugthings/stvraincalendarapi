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

    internal static bool IsMenuIcsPath(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        string value = path.Value!;
        return value.EndsWith("menu.ics", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/lunchmenu", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/breakfastmenu", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/academicmenu", StringComparison.OrdinalIgnoreCase)
            || IsFastLinkPath(value);
    }

    internal static bool IsFastLinkPath(string pathValue)
    {
        string trimmed = pathValue.Trim('/');
        if (trimmed.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        return FastLinkSlugGenerator.IsValidSlug(trimmed);
    }
}
