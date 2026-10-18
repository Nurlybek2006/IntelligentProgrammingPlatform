using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AcceptedCodeReviewsController : Controller
{
    private readonly AcceptedCodeReviewService _reviews;
    private readonly IStringLocalizer<SharedResource> _text;

    // Иелік тексеретін review қызметі мен қауіпсіз аударма хабарларын қабылдайды.
    public AcceptedCodeReviewsController(AcceptedCodeReviewService reviews, IStringLocalizer<SharedResource> text) { _reviews = reviews; _text = text; }

    [HttpPost]
    // CSRF тексерілген жеке сұраудан кейін Accepted нәтижесін өзгертпей шолу жасайды.
    public async Task<IActionResult> Analyze(long id, CancellationToken cancellationToken)
    {
        var result = await _reviews.AnalyzeAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        if (result == AcceptedCodeReviewStatus.NotFound) return NotFound();
        TempData["ReviewMessage"] = _text[$"Review_{result}"].Value;
        return RedirectToAction("Details", "Submissions", new { id });
    }
}
