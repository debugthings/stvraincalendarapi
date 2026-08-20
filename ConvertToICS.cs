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
        /// Azure Functions HTTP trigger (Kestrel uses <see cref="CreateMenu"/> below).
        /// </summary>
        [Function("createmenu")]
        [NonAction]
        public Task<IActionResult> CreateMenuFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{inputSession}menu.ics")] HttpRequest request,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] string buildingId = buildingId,
            [FromQuery] string districtId = districtId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuCoreAsync(request.HttpContext, inputSession, buildingId, districtId, startDate, endDate);

        /// <summary>
        /// Path-based calendar URL for clients that reject query strings (Google Calendar).
        /// </summary>
        [Function("createmenuByLocation")]
        [NonAction]
        public Task<IActionResult> CreateMenuByLocationFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{districtId}/{buildingId}/{inputSession}menu.ics")] HttpRequest request,
            [FromRoute] string districtId,
            [FromRoute] string buildingId,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuCoreAsync(request.HttpContext, inputSession, buildingId, districtId, startDate, endDate);

        /// <summary>
        /// Kestrel / LXC routes (Functions host uses <see cref="CreateMenuFunction"/>).
        /// </summary>
        [HttpGet("/{inputSession}menu.ics")]
        [HttpGet("/api/{inputSession}menu.ics")]
        public Task<IActionResult> CreateMenu(
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] string buildingId = buildingId,
            [FromQuery] string districtId = districtId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuCoreAsync(HttpContext, inputSession, buildingId, districtId, startDate, endDate);

        /// <summary>
        /// Google Calendar–friendly route: /{districtId}/{buildingId}/lunchmenu.ics
        /// </summary>
        [HttpGet("/{districtId}/{buildingId}/{inputSession}menu.ics")]
        [HttpGet("/api/{districtId}/{buildingId}/{inputSession}menu.ics")]
        public Task<IActionResult> CreateMenuByLocation(
            [FromRoute] string districtId,
            [FromRoute] string buildingId,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuCoreAsync(HttpContext, inputSession, buildingId, districtId, startDate, endDate);

        private async Task<IActionResult> CreateMenuCoreAsync(
            HttpContext httpContext,
            Session inputSession,
            string buildingId,
            string districtId,
            DateTime? startDate,
            DateTime? endDate)
        {
            try
            {
                return await _calendar.CreateMenuAsync(httpContext, inputSession, buildingId, districtId, startDate, endDate)
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
