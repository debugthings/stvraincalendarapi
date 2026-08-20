using Microsoft.AspNetCore.Mvc;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;

namespace StVrainToICSFunctionApp.Services;

public interface IMenuCalendarService
{
    Task<IActionResult> CreateMenuAsync(
        HttpContext httpContext,
        Session inputSession,
        string buildingId,
        string districtId,
        DateTime? startDate,
        DateTime? endDate);
}
