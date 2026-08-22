using Microsoft.AspNetCore.Mvc;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp;

[ApiController]
public sealed class LandingController : ControllerBase
{
    private readonly ILandingDayService _landing;

    public LandingController(ILandingDayService landing)
    {
        _landing = landing;
    }

    [HttpGet("/")]
    [HttpGet("/today")]
    public IActionResult Index()
    {
        return Content(LandingPageRenderer.Render(), "text/html; charset=utf-8");
    }

    [HttpGet("/settings")]
    public IActionResult Settings()
    {
        return Content(SettingsPageRenderer.Render(), "text/html; charset=utf-8");
    }

    [HttpPost("/api/landing/day")]
    public async Task<IActionResult> Day(
        [FromBody] LandingDayRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Schools is null || request.Schools.Count == 0)
        {
            return BadRequest(new { error = "At least one school is required." });
        }

        LandingDayResponse response = await _landing.BuildAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return Ok(response);
    }
}
