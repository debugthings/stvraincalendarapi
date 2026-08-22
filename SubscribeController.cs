using Microsoft.AspNetCore.Mvc;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp;

[ApiController]
public sealed class SubscribeController : ControllerBase
{
    private readonly ISubscribeService _subscribe;
    private readonly SchoolShortcutCatalog _shortcuts;

    public SubscribeController(ISubscribeService subscribe, SchoolShortcutCatalog shortcuts)
    {
        _subscribe = subscribe;
        _shortcuts = shortcuts;
    }

    [HttpGet("/subscribe")]
    public IActionResult Page([FromQuery] string? school, [FromQuery] string? buildingId)
    {
        int? defaultTime = null;
        string? preferredBuildingId = buildingId;
        if (!string.IsNullOrWhiteSpace(school) && _shortcuts.TryGet(school, out Options.SchoolShortcut shortcut))
        {
            preferredBuildingId = shortcut.BuildingId;
            defaultTime = shortcut.DefaultDisplayTime == 0 ? 1130 : shortcut.DefaultDisplayTime;
        }
        else if (!string.IsNullOrWhiteSpace(buildingId))
        {
            foreach (KeyValuePair<string, Options.SchoolShortcut> pair in _shortcuts.All())
            {
                if (string.Equals(pair.Value.BuildingId, buildingId, StringComparison.OrdinalIgnoreCase))
                {
                    defaultTime = pair.Value.DefaultDisplayTime == 0 ? 1130 : pair.Value.DefaultDisplayTime;
                    break;
                }
            }
        }

        string html = SubscribePageRenderer.Render(school, preferredBuildingId, defaultTime ?? 1130);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("/api/subscribe/schools")]
    public async Task<IActionResult> Schools(CancellationToken cancellationToken)
    {
        IReadOnlyList<SubscribeSchoolDto> schools = await _subscribe.GetSchoolsAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(schools);
    }

    [HttpGet("/api/subscribe/sections")]
    public async Task<IActionResult> Sections(
        [FromQuery] string buildingId,
        [FromQuery] string districtId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(buildingId) || string.IsNullOrWhiteSpace(districtId))
        {
            return BadRequest(new { error = "buildingId and districtId are required." });
        }

        IReadOnlyList<MenuSessionSectionDto> sections = await _subscribe
            .GetSectionsAsync(buildingId, districtId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(sections);
    }

    [HttpPost("/api/subscribe/create")]
    public async Task<IActionResult> Create(
        [FromBody] CreateFastLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            string baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            CreateFastLinkResponse created = await _subscribe
                .CreateFastLinkAsync(request, baseUrl, cancellationToken)
                .ConfigureAwait(false);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
