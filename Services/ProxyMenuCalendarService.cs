using Microsoft.AspNetCore.Mvc;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public sealed class ProxyMenuCalendarService : IMenuCalendarService
{
    private readonly ICalendarProxyService _proxy;

    public ProxyMenuCalendarService(ICalendarProxyService proxy)
    {
        _proxy = proxy;
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

        await _proxy.ProxyMenuRequestAsync(httpContext).ConfigureAwait(false);
        return new EmptyResult();
    }
}
