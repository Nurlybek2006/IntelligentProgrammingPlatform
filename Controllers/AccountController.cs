using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<SharedResource> _text;

    // Контроллерге дерекқор және қажетті қызметтерді береді.
    public AccountController(UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, ApplicationDbContext db, IStringLocalizer<SharedResource> text)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
        _text = text;
    }

    [AllowAnonymous, HttpGet]
    // Жаңа аккаунт тіркеу формасын көрсетеді.
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous, HttpPost]
    // Форманы тексеріп, жаңа Student аккаунтын жасайды.
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim();
        if (await _users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), _text["Account_DuplicateEmail"]);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = email, Email = email,
            DisplayName = model.DisplayName.Trim(), CreatedAt = DateTime.UtcNow
        };

        // Both user creation and Student assignment must succeed before issuing a cookie.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var result = await _users.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty,
                        _text[error.Code switch
                        {
                            "DuplicateEmail" or "DuplicateUserName" => "Account_DuplicateEmail",
                            "PasswordTooShort" or "PasswordRequiresNonAlphanumeric" or "PasswordRequiresDigit"
                                or "PasswordRequiresLower" or "PasswordRequiresUpper" or "PasswordRequiresUniqueChars" => "Identity_" + error.Code,
                            "InvalidEmail" or "InvalidUserName" => "Validation_Email",
                            _ => "Account_RegistrationFailed"
                        }]);
                return View(model);
            }

            var roleResult = await _users.AddToRoleAsync(user, RoleNames.Student);
            if (!roleResult.Succeeded)
            {
                ModelState.AddModelError(string.Empty, _text["Account_RegistrationFailed"]);
                return View(model);
            }
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, _text["Account_RegistrationConflict"]);
            return View(model);
        }

        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction(nameof(Profile));
    }

    [AllowAnonymous, HttpGet]
    // Пайдаланушыға жүйеге кіру бетін көрсетеді.
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginViewModel());
    }

    [AllowAnonymous, HttpPost]
    // Парольді тексеріп, қауіпсіз кіру cookie-сін жасайды.
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        if (!ModelState.IsValid)
            return View(model);

        var user = await _users.FindByEmailAsync(model.Email.Trim());
        var result = user == null ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, _text["Account_InvalidLogin"]);
            return View(model);
        }

        if (Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl!);
        return RedirectToAction("Index", "Tasks");
    }

    [HttpPost]
    // Ағымдағы пайдаланушының кіру cookie-сін жояды.
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Tasks");
    }

    [HttpGet]
    // Ағымдағы пайдаланушының қауіпсіз профиль деректерін көрсетеді.
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            await _signIn.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        return View(new ProfileViewModel
        {
            DisplayName = user.DisplayName, Email = user.Email ?? string.Empty,
            CreatedAt = user.CreatedAt, Roles = await _users.GetRolesAsync(user)
        });
    }

    [AllowAnonymous, HttpGet]
    // Рұқсаты жеткіліксіз пайдаланушыға 403 бетін қайтарады.
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
