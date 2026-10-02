using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class HintRevealsController : Controller
{
    private readonly HintRevealService _hints;

    // AI клиентіне тәуелсіз кеңес ашу қызметін қабылдайды.
    public HintRevealsController(HintRevealService hints) => _hints = hints;

    [HttpPost("/Submissions/{id:long}/RevealNextHint")]
    // CSRF қорғалған сұрауда иесінің келесі кеңесін ашып, нәтижеге қайтарады.
    public async Task<IActionResult> RevealNextHint(long id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();
        if (!await _hints.RevealNextAsync(id, userId, cancellationToken)) return NotFound();
        return RedirectToAction("Details", "Submissions", new { id });
    }
}
