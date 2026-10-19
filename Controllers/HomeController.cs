using System.Diagnostics;
using System.Security.Claims;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.ViewModels.Home;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace IntelligentProgrammingPlatform.Controllers;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class HomeController : Controller
{
    private readonly ProgressService _progress;
    private readonly ExperienceService _experience;
    private readonly LearningInsightsService _insights;
    private readonly UserManager<ApplicationUser> _users;

    // Басты бетке бар статистика мен оқу ұсынысын беретін қызметтерді қабылдайды.
    public HomeController(ProgressService progress, ExperienceService experience, LearningInsightsService insights, UserManager<ApplicationUser> users)
    {
        _progress = progress;
        _experience = experience;
        _insights = insights;
        _users = users;
    }

    // Қонаққа таныстыру бетін, студентке жеке соңғы нәтижелерін көрсетеді.
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return View(new HomeViewModel());
        // Бар қызметтердің дайын көрсеткіштерін тек басты беттің көрінісіне жинайды.
        var progress = await _progress.GetAsync(userId, cancellationToken);
        var experience = await _experience.GetAsync(userId, cancellationToken);
        var insights = await _insights.GetAsync(userId, cancellationToken);
        return View(new HomeViewModel
        {
            DisplayName = (await _users.GetUserAsync(User))?.DisplayName ?? string.Empty,
            SolvedTasks = progress.SolvedTasks, Score = progress.Score, SuccessRate = progress.SuccessRate,
            Experience = experience, PracticeNext = insights.PracticeNext, Recent = progress.Recent.Take(3).ToList()
        });
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
