using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.Leaderboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class LeaderboardController : Controller
{
    private readonly LeaderboardService _leaderboard;
    private readonly ILogger<LeaderboardController> _logger;

    // Әкімшілік қайта есептеу қызметін және қате журналын қабылдайды.
    public LeaderboardController(LeaderboardService leaderboard, ILogger<LeaderboardController> logger)
    {
        _leaderboard = leaderboard;
        _logger = logger;
    }

    [HttpPost]
    // CSRF қорғалған әкімші сұрауы арқылы барлық рейтинг жолдарын қайта есептейді.
    public async Task<IActionResult> Rebuild(CancellationToken cancellationToken)
    {
        try
        {
            await _leaderboard.RebuildAsync(cancellationToken);
            TempData["Success"] = "Leaderboard recalculated from submission history.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Leaderboard rebuild failed");
            TempData["Error"] = "Leaderboard recalculation is temporarily unavailable.";
        }
        return RedirectToAction("Index", "Home", new { area = "Admin" });
    }
}
