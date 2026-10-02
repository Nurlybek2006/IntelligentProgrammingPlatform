using IntelligentProgrammingPlatform.Services.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[AllowAnonymous, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CultureController : Controller
{
    [HttpPost]
    // CSRF тексерілген тіл таңдауын қауіпсіз cookie-ге сақтап, тек жергілікті бетке қайтарады.
    public IActionResult Set(string? culture, string? returnUrl)
    {
        if (!SupportedCultures.Contains(culture)) return BadRequest();
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture!)),
            new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
                IsEssential = true, Path = Request.PathBase.HasValue ? Request.PathBase.Value : "/",
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/"));
    }
}
