using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ProgressController : Controller
{
    private readonly ProgressService _progress;

    // Жеке статистиканы есептейтін қызметті қабылдайды.
    public ProgressController(ProgressService progress) => _progress = progress;

    [HttpGet]
    // Тек ағымдағы пайдаланушының прогресін көрсетеді.
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId == null ? Challenge() : View(await _progress.GetAsync(userId, cancellationToken));
    }
}
