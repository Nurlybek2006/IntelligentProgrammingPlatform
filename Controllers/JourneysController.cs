using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class JourneysController : Controller
{
    private readonly AttemptJourneyService _journeys;

    // Journey-дің тек иесіне арналған оқу қызметін қабылдайды.
    public JourneysController(AttemptJourneyService journeys) => _journeys = journeys;

    [HttpGet("/Tasks/{slug}/Journey")]
    // UserId-ді браузерден алмай, Identity иесінің толық әрекет жолын көрсетеді.
    public async Task<IActionResult> Index(string slug, int page = 1, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();
        var model = await _journeys.GetAsync(slug, userId, cancellationToken, page);
        return model == null ? NotFound() : View(model);
    }
}
