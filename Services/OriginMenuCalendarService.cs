using Microsoft.AspNetCore.Mvc;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public sealed class OriginMenuCalendarService : IMenuCalendarService
{
    private readonly IMenuCacheService _menuCache;
    private readonly double _defaultStart;
    private readonly double _defaultEnd;

    public OriginMenuCalendarService(IMenuCacheService menuCache, IConfiguration configuration)
    {
        _menuCache = menuCache;
        _defaultStart = configuration.GetValue<double?>("DefaultStartOffset") ?? -7.0;
        _defaultEnd = configuration.GetValue<double?>("DefaultEndOffset") ?? 30.0;
    }

    public async Task<IActionResult> CreateMenuAsync(
        HttpContext httpContext,
        Session inputSession,
        string buildingId,
        string districtId,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (inputSession == Session.None)
        {
            return new NotFoundResult();
        }

        httpContext.Items[ICSTextOutputFormatter.inputSessionContext] = inputSession;
        httpContext.Response.ContentType = "text/calendar";

        startDate ??= DateTime.Now.AddDays(_defaultStart);
        endDate ??= DateTime.Now.AddDays(_defaultEnd);

        Menu menu = await _menuCache.GetMenuAsync(buildingId, districtId, startDate.Value, endDate.Value)
            .ConfigureAwait(false);
        return new OkObjectResult(menu);
    }
}
