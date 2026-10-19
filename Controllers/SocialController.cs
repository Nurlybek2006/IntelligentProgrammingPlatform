using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class SocialController : Controller
{
    private readonly SocialNotificationService _notifications;

    // Тек кіріс санағыштарын беретін әлеуметтік қызметті қабылдайды.
    public SocialController(SocialNotificationService notifications) => _notifications = notifications;

    [HttpGet]
    // URL не формадағы UserId-ді қолданбай, ағымдағы аккаунттың санағыштарын қайтарады.
    public async Task<IActionResult> Counts(CancellationToken cancellationToken) =>
        Json(await _notifications.GetCountsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken));
}
