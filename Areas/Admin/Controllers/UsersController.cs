using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.Profile;
using IntelligentProgrammingPlatform.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class UsersController : Controller
{
    private readonly AdminUsersService _users;
    private readonly ProfileService _profiles;
    private readonly IStringLocalizer<AdminResource> _text;

    // Пайдаланушылар тізімі мен аватар модерациясының қызметтерін қабылдайды.
    public UsersController(AdminUsersService users, ProfileService profiles, IStringLocalizer<AdminResource> text)
    {
        _users = users; _profiles = profiles; _text = text;
    }

    [HttpGet]
    // Пайдаланушыларды жиырмадан беттеп көрсетеді.
    public async Task<IActionResult> Index(int page, CancellationToken cancellationToken) =>
        View(await _users.GetAsync(page, cancellationToken));

    [HttpGet]
    // Таңдалған пайдаланушының қауіпсіз профилі мен статистикасын көрсетеді.
    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetAsync(id, cancellationToken);
        return profile == null ? NotFound() : View(new UserDetailsViewModel { Id = id, Profile = profile });
    }

    [HttpPost]
    // Әкімшіге тек сақталған аватарды алып тастауға рұқсат береді.
    public async Task<IActionResult> RemoveAvatar(string id, CancellationToken cancellationToken)
    {
        if (await _profiles.GetAsync(id, cancellationToken) == null) return NotFound();
        var success = await _profiles.RemoveAvatarAsync(id);
        TempData[success ? "Success" : "ProfileError"] = _text[success ? "User_AvatarRemoved" : "User_SaveFailed"].Value;
        return RedirectToAction(nameof(Details), new { id });
    }
}
