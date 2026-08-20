using Microsoft.AspNetCore.Mvc;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp
{
    [ApiController]
    public class ConvertToICS : ControllerBase
    {
        private readonly double defaultStart;
        private readonly double defaultEnd;
        private const string buildingId = "67673211-c4be-ed11-82b1-880d996bcdd8";
        private const string districtId = "55485575-09b2-ed11-8e69-f29174b2df22";

        private readonly ILogger<ConvertToICS> _logger;
        private readonly IMenuCacheService _menuCache;

        public ConvertToICS(ILogger<ConvertToICS> logger, IMenuCacheService menuCache, IConfiguration configuration)
        {
            _logger = logger;
            _menuCache = menuCache;
            defaultStart = configuration.GetValue<double?>("DefaultStartOffset") ?? -7.0;
            defaultEnd = configuration.GetValue<double?>("DefaultEndOffset") ?? 30.0;
        }

        /// <summary>
        /// Gets the menu from the supplied parameters and returns it as iCalendar.
        /// </summary>
        /// <remarks>
        /// To get the GUIDs needed, open the LINQ website, e.g.
        /// https://linqconnect.com/public/menu/DCN3CB?buildingId=67673211-c4be-ed11-82b1-880d996bcdd8
        /// </remarks>
        [HttpGet("/{inputSession}menu.ics")]
        [HttpGet("/api/{inputSession}menu.ics")]
        public async Task<IActionResult> CreateMenu(
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] string buildingId = buildingId,
            [FromQuery] string districtId = districtId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (inputSession == Session.None)
            {
                return NotFound();
            }

            HttpContext.Items[ICSTextOutputFormatter.inputSessionContext] = inputSession;
            return await GenerateICalResponse(buildingId, districtId, startDate, endDate);
        }

        private async Task<IActionResult> GenerateICalResponse(string buildingId, string districtId, DateTime? startDate, DateTime? endDate)
        {
            Response.ContentType = "text/calendar";
            return new OkObjectResult(await GetTheCalendar(buildingId, districtId, startDate, endDate));
        }

        private async Task<Menu> GetTheCalendar(string buildingId = buildingId, string districtId = districtId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.Now.AddDays(defaultStart);
            endDate ??= DateTime.Now.AddDays(defaultEnd);

            try
            {
                return await _menuCache.GetMenuAsync(buildingId, districtId, startDate.Value, endDate.Value)
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
