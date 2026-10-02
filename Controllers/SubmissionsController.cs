using System.Security.Claims;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.Services.Submissions;
using IntelligentProgrammingPlatform.Services.AI;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class SubmissionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SubmissionService _submissions;
    private readonly TaskPageService _pages;
    private readonly ILogger<SubmissionsController> _logger;
    private readonly OpenAiTutorService _ai;
    private readonly IStringLocalizer<SharedResource> _text;

    // Контроллерге тексеру, қауіпсіз бет құру және сақтау қызметтерін береді.
    public SubmissionsController(ApplicationDbContext db, SubmissionService submissions, TaskPageService pages,
        ILogger<SubmissionsController> logger, OpenAiTutorService ai, IStringLocalizer<SharedResource> text)
    {
        _db = db;
        _submissions = submissions;
        _pages = pages;
        _logger = logger;
        _ai = ai;
        _text = text;
    }

    [HttpPost, RequestSizeLimit(512 * 1024)]
    // Ағымдағы Identity пайдаланушысының кодын тексеруге қабылдайды.
    public async Task<IActionResult> Submit(SubmitViewModel model, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();
        var slug = await _db.ProgrammingTasks.Where(task => task.Id == model.ProgrammingTaskId && task.IsPublished)
            .Select(task => task.Slug).SingleOrDefaultAsync(cancellationToken);
        if (slug == null) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var outcome = await _submissions.SubmitAsync(userId, model, cancellationToken);
                if (outcome.Id.HasValue)
                    return RedirectToAction(nameof(Details), new { id = outcome.Id.Value });
                ModelState.AddModelError(string.Empty, _text[outcome.Error ?? "Runner_Unavailable"]);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not accept a submission");
                ModelState.AddModelError(string.Empty, _text["Runner_Unavailable"]);
            }
        }
        var page = await _pages.GetAsync(slug, model, cancellationToken, userId);
        return page == null ? NotFound() : View("~/Views/Tasks/Details.cshtml", page);
    }

    [HttpGet]
    // Тек ағымдағы пайдаланушы жіберілімдерін жаңасынан бастап беттеп көрсетеді.
    public async Task<IActionResult> My(int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 100000);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var items = await _db.Submissions.AsNoTracking().Where(submission => submission.UserId == userId)
            .OrderByDescending(submission => submission.CreatedAt).ThenByDescending(submission => submission.Id)
            .Skip((page - 1) * 20).Take(21)
            .Select(submission => new SubmissionListItemViewModel
            {
                Id = submission.Id, TaskTitle = submission.ProgrammingTask.Title,
                RuntimeName = submission.Runtime.Name, Status = submission.Status,
                PassedTests = submission.PassedTests, TotalTests = submission.TotalTests, CreatedAt = submission.CreatedAt
            }).ToListAsync(cancellationToken);
        return View(new SubmissionHistoryViewModel { Page = page, HasNextPage = items.Count > 20, Items = items.Take(20).ToList() });
    }

    [HttpGet]
    // Иесінің жіберілімін ғана оқып, hidden тест деректерін SQL проекциясында алып тастайды.
    public async Task<IActionResult> Details(long id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = await _db.Submissions.AsNoTracking()
            .Where(submission => submission.Id == id && submission.UserId == userId)
            .Select(submission => new SubmissionDetailsViewModel
            {
                Id = submission.Id, TaskTitle = submission.ProgrammingTask.Title, TaskSlug = submission.ProgrammingTask.Slug,
                RuntimeName = submission.Runtime.Name, SourceCode = submission.SourceCode, Status = submission.Status,
                CompileSucceeded = submission.CompileSucceeded, CompilerOutput = submission.CompilerOutput,
                PassedTests = submission.PassedTests, TotalTests = submission.TotalTests,
                ExecutionTimeMs = submission.ExecutionTimeMs, CreatedAt = submission.CreatedAt,
                StartedAt = submission.StartedAt, FinishedAt = submission.FinishedAt,
                Results = submission.ExecutionResults.OrderBy(result => result.TestCase.Order).Select(result => new TestResultViewModel
                {
                    Number = result.TestCase.Order, IsHidden = result.TestCase.IsHidden,
                    Status = result.Status, ExecutionTimeMs = result.ExecutionTimeMs,
                    MemoryUsedKb = result.TestCase.IsHidden ? null : result.MemoryUsedKb,
                    Input = result.TestCase.IsHidden ? null : result.TestCase.Input,
                    ExpectedOutput = result.TestCase.IsHidden ? null : result.TestCase.ExpectedOutput,
                    ActualOutput = result.TestCase.IsHidden ? null : result.ActualOutput,
                    ErrorMessage = result.TestCase.IsHidden ? null : result.ErrorMessage
                }).ToList()
            }).SingleOrDefaultAsync(cancellationToken);
        if (model == null) return NotFound();
        model.AiConfigured = _ai.IsConfigured;
        model.AiFeedback = await _ai.GetExistingAsync(id, userId!, cancellationToken);
        return View(model);
    }

    [HttpGet]
    // Екі кодты тек бір иеге және бір есепке тиесілі болғанда хронологиялық ретпен салыстырады.
    public async Task<IActionResult> Compare(long olderId, long newerId, CancellationToken cancellationToken)
    {
        if (olderId <= 0 || newerId <= 0 || olderId == newerId) return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var attempts = await _db.Submissions.AsNoTracking()
            .Where(item => item.UserId == userId && (item.Id == olderId || item.Id == newerId))
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new ComparedAttemptViewModel
            {
                Id = item.Id, ProgrammingTaskId = item.ProgrammingTaskId, TaskSlug = item.ProgrammingTask.Slug,
                TaskTitle = item.ProgrammingTask.Title, SourceCode = item.SourceCode, Status = item.Status,
                CreatedAt = item.CreatedAt, ExecutionTimeMs = item.ExecutionTimeMs,
                RuntimeName = item.Runtime.Name, LanguageKey = item.Runtime.LanguageKey
            }).ToListAsync(cancellationToken);
        if (attempts.Count != 2 || attempts[0].ProgrammingTaskId != attempts[1].ProgrammingTaskId)
            return NotFound();
        var language = attempts[0].LanguageKey == attempts[1].LanguageKey
            ? RunnerLanguage.Find(attempts[0].LanguageKey)?.Key : null;
        return View(new SubmissionComparisonViewModel
        {
            Previous = attempts[0], Current = attempts[1], LanguageKey = language ?? "plaintext"
        });
    }

    [HttpPost]
    // Иесінің жіберіліміне AI талдауын тек CSRF қорғалған айқын сұраумен бастайды.
    public async Task<IActionResult> Analyze(long id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Challenge();
        var result = await _ai.AnalyzeAsync(id, userId, cancellationToken);
        if (result.Status == AiAnalysisStatus.NotFound) return NotFound();
        TempData["AiMessage"] = _text[result.Status switch
        {
            AiAnalysisStatus.Saved => "AiMessage_Saved",
            AiAnalysisStatus.Existing => "AiMessage_Existing",
            AiAnalysisStatus.NotFinished => "AiMessage_NotFinished",
            AiAnalysisStatus.NotConfigured => "AiMessage_NotConfigured",
            AiAnalysisStatus.Busy => "AiMessage_Busy",
            _ => "AiMessage_Unavailable"
        }].Value;
        return RedirectToAction(nameof(Details), new { id });
    }
}
