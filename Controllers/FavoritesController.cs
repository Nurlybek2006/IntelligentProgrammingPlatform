using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class FavoritesController : Controller
{
    private readonly TaskFavoriteService _favorites;

    // Таңдаулыларға тек иесі арқылы қатынайтын қызметті қабылдайды.
    public FavoritesController(TaskFavoriteService favorites) => _favorites = favorites;

    [HttpGet("/Tasks/Favorites")]
    // Ағымдағы аккаунттың жарияланған таңдаулы есептерін көрсетеді.
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await _favorites.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken));

    [HttpPost]
    // Таңдаулыны Identity иесіне ғана қосады және жергілікті бетке қайтарады.
    public async Task<IActionResult> Add(int taskId, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!await _favorites.SetAsync(taskId, User.FindFirstValue(ClaimTypes.NameIdentifier)!, true, cancellationToken)) return NotFound();
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!);
    }

    [HttpPost]
    // Тек ағымдағы аккаунттың белгісін өшіреді; басқа иенің Id өрісі қабылданбайды.
    public async Task<IActionResult> Remove(int taskId, string? returnUrl, CancellationToken cancellationToken)
    {
        await _favorites.SetAsync(taskId, User.FindFirstValue(ClaimTypes.NameIdentifier)!, false, cancellationToken);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!);
    }
}
