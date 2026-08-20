namespace StVrainToICSFunctionApp.Services;

public interface ICalendarProxyService
{
    Task ProxyMenuRequestAsync(HttpContext context);
}
