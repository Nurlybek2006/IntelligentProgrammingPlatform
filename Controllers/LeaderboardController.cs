using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Leaderboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[AllowAnonymous, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class LeaderboardController : Controller
{
    private readonly LeaderboardService _leaderboard;

    // Ашық рейтингті оқитын қызметті қабылдайды.
    public LeaderboardController(LeaderboardService leaderboard) => _leaderboard = leaderboard;

    [HttpGet]
    // Жеке деректерсіз тұрақты реттелген рейтинг бетін көрсетеді.
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(await _leaderboard.GetPageAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), page, cancellationToken));
}
