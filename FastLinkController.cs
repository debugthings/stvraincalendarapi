using Microsoft.AspNetCore.Mvc;
using StVrainToICSFunctionApp.Data;
using StVrainToICSFunctionApp.Formatters;
using StVrainToICSFunctionApp.Models;
using StVrainToICSFunctionApp.Services;

namespace StVrainToICSFunctionApp;

[ApiController]
public sealed class FastLinkController : ControllerBase
{
    private readonly IFastLinkStore _fastLinks;
    private readonly IMenuCalendarService _calendar;

    public FastLinkController(IFastLinkStore fastLinks, IMenuCalendarService calendar)
    {
        _fastLinks = fastLinks;
        _calendar = calendar;
    }

    [HttpGet("/{slug:regex(^[[a-z]]+-[[a-z]]+$)}")]
    [HttpGet("/{slug:regex(^[[a-z]]+-[[a-z]]+$)}.ics")]
    public async Task<IActionResult> Get(
        [FromRoute] string slug,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        string normalized = slug;
        if (normalized.EndsWith(".ics", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        if (!FastLinkSlugGenerator.IsValidSlug(normalized))
        {
            return NotFound();
        }

        FastLinkEntry? entry = await _fastLinks.GetAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return NotFound();
        }

        if (!Enum.TryParse(entry.Session, out Session session) || session == Session.None)
        {
            session = Session.Lunch;
        }

        HttpContext.Items[ICSTextOutputFormatter.displayTimeHhmmContext] = entry.DisplayTimeHhmm;
        HttpContext.Items[ICSTextOutputFormatter.sectionFilterContext] =
            FastLinkStore.DeserializeFilter(entry.IncludedPlansJson);

        return await _calendar
            .CreateMenuAsync(HttpContext, session, entry.BuildingId, entry.DistrictId, startDate, endDate)
            .ConfigureAwait(false);
    }
}
