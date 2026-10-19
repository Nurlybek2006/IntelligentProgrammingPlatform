using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Social;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.ViewComponents;

public sealed class SocialNavigationViewComponent : ViewComponent
{
    private readonly SocialNotificationService _notifications;

    // Навигацияға иесінің SQL санағыштарын беретін қызметті қабылдайды.
    public SocialNavigationViewComponent(SocialNotificationService notifications) => _notifications = notifications;

    // Әр беттің бастапқы unread және кіріс сұрау санын JavaScript-ке тәуелсіз көрсетеді.
    public async Task<IViewComponentResult> InvokeAsync() =>
        View(await _notifications.GetCountsAsync(UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)!, HttpContext.RequestAborted));
}
