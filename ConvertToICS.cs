using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp
{
    [ApiController]
    public class ConvertToICS : ControllerBase
    {
        private const string buildingId = "67673211-c4be-ed11-82b1-880d996bcdd8";
        private const string districtId = "55485575-09b2-ed11-8e69-f29174b2df22";

        private readonly ILogger<ConvertToICS> _logger;
        private readonly IMenuCalendarService _calendar;

        public ConvertToICS(ILogger<ConvertToICS> logger, IMenuCalendarService calendar)
        {
            _logger = logger;
            _calendar = calendar;
        }

        /// <summary>
        /// Gets the menu from the supplied parameters and returns it as iCalendar.
        /// </summary>
        [Function("createmenu")]
        [HttpGet("/{inputSession}menu.ics")]
        [HttpGet("/api/{inputSession}menu.ics")]
        public async Task<IActionResult> CreateMenu(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{inputSession}menu.ics")] HttpRequest _,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] string buildingId = buildingId,
            [FromQuery] string districtId = districtId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                return await _calendar.CreateMenuAsync(HttpContext, inputSession, buildingId, districtId, startDate, endDate)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while getting the calendar from the api endpoint.");
                throw;
            }
        }
    }
}
