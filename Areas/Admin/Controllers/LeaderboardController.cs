using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.Leaderboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class LeaderboardController : Controller
{
    private readonly LeaderboardService _leaderboard;
    private readonly ILogger<LeaderboardController> _logger;
    private readonly IStringLocalizer<AdminResource> _text;

    // Әкімшілік қайта есептеу қызметін және қате журналын қабылдайды.
    public LeaderboardController(LeaderboardService leaderboard, ILogger<LeaderboardController> logger,
        IStringLocalizer<AdminResource> text)
    {
        _leaderboard = leaderboard;
        _logger = logger;
        _text = text;
    }

    [HttpPost]
    // CSRF қорғалған әкімші сұрауы арқылы барлық рейтинг жолдарын қайта есептейді.
    public async Task<IActionResult> Rebuild(CancellationToken cancellationToken)
    {
        try
        {
            await _leaderboard.RebuildAsync(cancellationToken);
            TempData["Success"] = _text["Leaderboard_Rebuilt"].Value;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Leaderboard rebuild failed");
            TempData["Error"] = _text["Leaderboard_Unavailable"].Value;
        }
        return RedirectToAction("Index", "Home", new { area = "Admin" });
    }
}
