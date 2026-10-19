using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[AllowAnonymous, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class UsersController : Controller
{
    private readonly SocialProfileService _profiles;

    // Ашық профильге жеке аккаунт моделін бермейтін қызметті қабылдайды.
    public UsersController(SocialProfileService profiles) => _profiles = profiles;

    [HttpGet("/Users/{id}")]
    // Қауіпсіз оқу қорытындысы мен ағымдағы көрерменге тиісті әлеуметтік батырмаларды көрсетеді.
    public async Task<IActionResult> Profile(string id, CancellationToken cancellationToken)
    {
        var model = await _profiles.GetProfileAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        return model == null ? NotFound() : View("Details", model);
    }
}
