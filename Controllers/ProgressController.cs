using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ProgressController : Controller
{
    private readonly ProgressService _progress;
    private readonly LearningInsightsService _insights;
    private readonly ExperienceService _experience;

    // Жеке статистиканы есептейтін қызметті қабылдайды.
    public ProgressController(ProgressService progress, LearningInsightsService insights, ExperienceService experience)
    {
        _progress = progress;
        _insights = insights;
        _experience = experience;
    }

    [HttpGet]
    // Тек ағымдағы пайдаланушының прогресін көрсетеді.
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();
        var model = await _progress.GetAsync(userId, cancellationToken);
        model.LearningMap = await _insights.GetAsync(userId, cancellationToken);
        model.Experience = await _experience.GetAsync(userId, cancellationToken);
        return View(model);
    }
}
