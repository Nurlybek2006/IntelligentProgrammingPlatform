using System.Diagnostics;
using System.Security.Claims;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentProgrammingPlatform.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class HomeController : Controller
{
    private readonly ProgressService _progress;

    // Басты бетке жеңіл статистика дайындайтын қызметті қабылдайды.
    public HomeController(ProgressService progress) => _progress = progress;

    // Қонаққа таныстыру бетін, студентке жеке соңғы нәтижелерін көрсетеді.
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View(userId == null ? new HomeViewModel() : await _progress.GetHomeAsync(userId, cancellationToken));
    }

    // Пайдаланушы деректерінің қолданылуын қысқаша түсіндіреді.
    public IActionResult Privacy() => View();

    [IgnoreAntiforgeryToken]
    // Ішкі exception деректерін ашпай, күй өзгертпейтін қауіпсіз 500 бетін көрсетеді.
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View(new ErrorViewModel { StatusCode = 500, RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [IgnoreAntiforgeryToken]
    // CSRF қатесін де күй өзгертпей көрсетіп, бастапқы HTTP мәртебесін сақтайды.
    public IActionResult HttpError(int code = 404)
    {
        var status = code is >= 400 and <= 599 ? code : 404;
        Response.StatusCode = status;
        return View("Error", new ErrorViewModel { StatusCode = status });
    }
}
