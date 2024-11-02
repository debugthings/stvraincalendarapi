using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using StVrainToICSFunctionApp.Models;
using static StVrainToICSFunctionApp.Helpers.Helpers;
using StVrainToICSFunctionApp.Formatters;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;

namespace StVrainToICSFunctionApp
{
    public class ConvertToICS
    {
        private double defaultStart = GetEnvironmentVariable<double>("DefaultStartOffset");
        private double defaultEnd = GetEnvironmentVariable<double>("DefaultEndOffset");        
        private const string buildingId = "67673211-c4be-ed11-82b1-880d996bcdd8";
        private const string districtId = "55485575-09b2-ed11-8e69-f29174b2df22";

        private readonly ILogger<TelemetryClient> _logger;
        private readonly IHttpClientFactory _clientFactory;

        public ConvertToICS(ILogger<TelemetryClient> logger, IHttpClientFactory clientFactory)
        {
            _logger = logger;
            this._clientFactory = clientFactory;
        }

        /// <summary>
        /// Gets the menu from the supplied parameters.
        /// </summary>
        /// <param name="req">The http request sent in from Azure Functions.</param>
        /// <param name="inputSession">The type of menu we're after.</param>
        /// <param name="buildingId">The guid of the building id.</param>
        /// <param name="districtId">The guid of the district id.</param>
        /// <param name="startDate">The start date of the menu to fetch.</param>
        /// <param name="endDate">The end date of the menu to fetch.</param>
        /// <returns>A formatted iCal version of the menu.</returns>
        /// <remarks>
        /// This function reads the LINQ menu JSON file and returns the iCal formattd version of it. To get the GUIDS needed you will need to go to the LINQ website to find them. https://linqconnect.com/public/menu/DCN3CB?buildingId=67673211-c4be-ed11-82b1-880d996bcdd8 
        /// </remarks>
        [Function("createmenu")]
        public async Task<IActionResult> CreateMenu([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{inputSession}menu.ics")] HttpRequest req,
            [FromRoute] Session inputSession = Session.None,
            string buildingId = buildingId,
            string districtId = districtId,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            if (inputSession == Session.None)
            {
                return new NotFoundResult();
            }
            req.HttpContext.Items[ICSTextOutputFormatter.inputSessionContext] = inputSession;
            return await GenerateICalResponse(req, buildingId, districtId, startDate, endDate);
        }

        private async Task<IActionResult> GenerateICalResponse(HttpRequest req, string buildingId, string districtId, DateTime? startDate, DateTime? endDate)
        {
            req.HttpContext.Response.ContentType = "text/calendar";

            var formattedMenu = new OkObjectResult(await GetTheCalendar(buildingId, districtId, startDate, endDate));
            return formattedMenu;
        }

        private async Task<Menu> GetTheCalendar(string buildingId = buildingId, string districtId = districtId, DateTime? startDate = null, DateTime? endDate = null)
        {
            Menu menu;
            try
            {
                var client = this._clientFactory.CreateClient("LINQ");
                defaultStart = defaultStart == 0.0 ? -7.0 : defaultStart;
                defaultEnd = defaultEnd == 0.0 ? 30.0 : defaultEnd;

                startDate ??= DateTime.Now.AddDays(defaultStart);
                endDate ??= DateTime.Now.AddDays(defaultEnd);

                menu = await client.GetFromJsonAsync<Menu>($"/api/FamilyMenu?buildingId={buildingId}&districtId={districtId}&startDate={startDate:M-dd-yyyy}&endDate={endDate:M-dd-yyyy}");

                if (menu == null)
                {
                    throw new NullReferenceException($"The menu api for district {districtId}, building {buildingId}, for the time range {startDate:M-dd-yyyy} to {endDate:M-dd-yyyy} was null. Check the parameters and try again.");
                }
            }
            catch (NullReferenceException nre)
            {
                this._logger.LogError(nre, $"Exception while geting the calendar from the api endpoint.");
                throw;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, $"Exception while geting the calendar from the api endpoint.");
                throw;
            }

            return menu;
        }
    }
}
