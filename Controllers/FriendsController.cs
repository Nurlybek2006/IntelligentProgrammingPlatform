using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class FriendsController : Controller
{
    private readonly FriendshipService _friends;
    private readonly SocialProfileService _profiles;
    private readonly IStringLocalizer<SharedResource> _text;

    // Достық ережесін, қауіпсіз карточкаларды және аудармаларды қабылдайды.
    public FriendsController(FriendshipService friends, SocialProfileService profiles, IStringLocalizer<SharedResource> text)
    { _friends = friends; _profiles = profiles; _text = text; }

    [HttpGet]
    // Иесінің достарын, сұрауларын және DisplayName бойынша іздеуді беттеп көрсетеді.
    public async Task<IActionResult> Index(string? section, string? q, int page = 1, CancellationToken cancellationToken = default) =>
        View(await _profiles.GetFriendsAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, section, q, page, cancellationToken));

    [HttpPost, ActionName("Request"), EnableRateLimiting(SocialRateLimits.Requests)]
    // Ағымдағы аккаунт атынан ғана жаңа достық сұрауын жібереді.
    public Task<IActionResult> SendRequest(string targetUserId, string? returnUrl, CancellationToken cancellationToken) => Change(targetUserId, returnUrl, FriendshipAction.Request, cancellationToken);

    [HttpPost, EnableRateLimiting(SocialRateLimits.Actions)]
    // Кіріс сұрауын тек алушының қабылдауына мүмкіндік береді.
    public Task<IActionResult> Accept(string targetUserId, string? returnUrl, CancellationToken cancellationToken) => Change(targetUserId, returnUrl, FriendshipAction.Accept, cancellationToken);

    [HttpPost, EnableRateLimiting(SocialRateLimits.Actions)]
    // Кіріс Pending сұрауын тек алушы атынан қабылдамайды.
    public Task<IActionResult> Decline(string targetUserId, string? returnUrl, CancellationToken cancellationToken) => Change(targetUserId, returnUrl, FriendshipAction.Decline, cancellationToken);

    [HttpPost, EnableRateLimiting(SocialRateLimits.Actions)]
    // Өзінің әлі қабылданбаған шығыс сұрауын кері алады.
    public Task<IActionResult> Cancel(string targetUserId, string? returnUrl, CancellationToken cancellationToken) => Change(targetUserId, returnUrl, FriendshipAction.Cancel, cancellationToken);

    [HttpPost, EnableRateLimiting(SocialRateLimits.Actions)]
    // Достықты алып тастайды, бірақ жеке хат тарихына тимейді.
    public Task<IActionResult> Remove(string targetUserId, string? returnUrl, CancellationToken cancellationToken) => Change(targetUserId, returnUrl, FriendshipAction.Remove, cancellationToken);

    [HttpPost, EnableRateLimiting(SocialRateLimits.Actions)]
    // Өз атынан блок қойып, жұптың достығын бір транзакцияда тоқтатады.
    public Task<IActionResult> Block(string targetUserId, string? returnUrl, CancellationToken cancellationToken) => Change(targetUserId, returnUrl, FriendshipAction.Block, cancellationToken);

    [HttpPost, EnableRateLimiting(SocialRateLimits.Actions)]
    // Тек өзінің блогын алып тастайды; достық автоматты қалпына келмейді.
    public Task<IActionResult> Unblock(string targetUserId, string? returnUrl, CancellationToken cancellationToken) => Change(targetUserId, returnUrl, FriendshipAction.Unblock, cancellationToken);

    // Әр POST-қа иесін claims-тен алып, тек жергілікті мекенжайға қайтарады.
    private async Task<IActionResult> Change(string targetUserId, string? returnUrl, FriendshipAction action, CancellationToken cancellationToken)
    {
        var result = await _friends.ChangeAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, targetUserId, action, cancellationToken);
        if (!result.Succeeded) return SocialResponses.Failure(this, _text, result.Outcome);
        TempData["Success"] = _text["Social_Updated"].Value;
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!);
    }
}
