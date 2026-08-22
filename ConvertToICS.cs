using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Options;
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
        private readonly SchoolShortcutCatalog _schools;

        public ConvertToICS(ILogger<ConvertToICS> logger, IMenuCalendarService calendar, SchoolShortcutCatalog schools)
        {
            _logger = logger;
            _calendar = calendar;
            _schools = schools;
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
        /// Path-based calendar URL with an absolute display time as HHmm (1100, 1130, 1200).
        /// </summary>
        [Function("createmenuByLocationTime")]
        [NonAction]
        public Task<IActionResult> CreateMenuByLocationTimeFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{districtId}/{buildingId}/{displayTime}/{inputSession}menu.ics")] HttpRequest request,
            [FromRoute] string districtId,
            [FromRoute] string buildingId,
            [FromRoute] int displayTime,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuCoreAsync(request.HttpContext, inputSession, buildingId, districtId, startDate, endDate, displayTime);

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

        /// <summary>
        /// Absolute display time as HHmm. Example: /{districtId}/{buildingId}/1200/lunchmenu.ics → 12:00.
        /// </summary>
        [HttpGet("/{districtId}/{buildingId}/{displayTime:int}/{inputSession}menu.ics")]
        [HttpGet("/api/{districtId}/{buildingId}/{displayTime:int}/{inputSession}menu.ics")]
        public Task<IActionResult> CreateMenuByLocationTime(
            [FromRoute] string districtId,
            [FromRoute] string buildingId,
            [FromRoute] int displayTime,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuCoreAsync(HttpContext, inputSession, buildingId, districtId, startDate, endDate, displayTime);

        /// <summary>
        /// Short school codes for Google Calendar (no GUIDs): /rhe/lunchmenu, /ems/breakfastmenu.
        /// </summary>
        [Function("createmenuBySchool")]
        [NonAction]
        public Task<IActionResult> CreateMenuBySchoolFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{school}/{inputSession}menu.ics")] HttpRequest request,
            [FromRoute] string school,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuBySchoolAsync(request.HttpContext, school, inputSession, startDate, endDate);

        [Function("createmenuBySchoolBare")]
        [NonAction]
        public Task<IActionResult> CreateMenuBySchoolBareFunction(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{school}/{inputSession}menu")] HttpRequest request,
            [FromRoute] string school,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuBySchoolAsync(request.HttpContext, school, inputSession, startDate, endDate);

        [HttpGet("/{school:regex(^[[a-zA-Z]]{{2,8}}$)}/{inputSession}menu")]
        [HttpGet("/{school:regex(^[[a-zA-Z]]{{2,8}}$)}/{inputSession}menu.ics")]
        [HttpGet("/api/{school:regex(^[[a-zA-Z]]{{2,8}}$)}/{inputSession}menu")]
        [HttpGet("/api/{school:regex(^[[a-zA-Z]]{{2,8}}$)}/{inputSession}menu.ics")]
        public Task<IActionResult> CreateMenuBySchool(
            [FromRoute] string school,
            [FromRoute] Session inputSession = Session.None,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null) =>
            CreateMenuBySchoolAsync(HttpContext, school, inputSession, startDate, endDate);

        private Task<IActionResult> CreateMenuBySchoolAsync(
            HttpContext httpContext,
            string school,
            Session inputSession,
            DateTime? startDate,
            DateTime? endDate)
        {
            if (inputSession is not Session.Lunch and not Session.Breakfast and not Session.Academic
                || !_schools.TryGet(school, out SchoolShortcut shortcut)
                || string.IsNullOrWhiteSpace(shortcut.BuildingId)
                || string.IsNullOrWhiteSpace(shortcut.DistrictId))
            {
                return Task.FromResult<IActionResult>(new NotFoundResult());
            }

            int? displayTime = inputSession switch
            {
                Session.Lunch when shortcut.DefaultDisplayTime != 0 => shortcut.DefaultDisplayTime,
                Session.Breakfast => shortcut.DefaultBreakfastDisplayTime == 0 ? 830 : shortcut.DefaultBreakfastDisplayTime,
                _ => null,
            };
            return CreateMenuCoreAsync(
                httpContext,
                inputSession,
                shortcut.BuildingId,
                shortcut.DistrictId,
                startDate,
                endDate,
                displayTime);
        }

        private async Task<IActionResult> CreateMenuCoreAsync(
            HttpContext httpContext,
            Session inputSession,
            string buildingId,
            string districtId,
            DateTime? startDate,
            DateTime? endDate,
            int? displayTimeHhmm = null)
        {
            if (displayTimeHhmm is int hhmm)
            {
                if (!ICSTextOutputFormatter.TryGetClockTime(hhmm, out _, out _))
                {
                    return new NotFoundResult();
                }

                httpContext.Items[ICSTextOutputFormatter.displayTimeHhmmContext] = hhmm;
            }

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
