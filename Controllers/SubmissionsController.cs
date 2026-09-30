using System.Security.Claims;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.Services.Submissions;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class SubmissionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SubmissionService _submissions;
    private readonly TaskPageService _pages;
    private readonly ILogger<SubmissionsController> _logger;

    // Контроллерге тексеру, қауіпсіз бет құру және сақтау қызметтерін береді.
    public SubmissionsController(ApplicationDbContext db, SubmissionService submissions, TaskPageService pages,
        ILogger<SubmissionsController> logger)
    {
        _db = db;
        _submissions = submissions;
        _pages = pages;
        _logger = logger;
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
                ModelState.AddModelError(string.Empty, outcome.Error ?? CodeRunnerOptions.UnavailableMessage);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not accept a submission");
                ModelState.AddModelError(string.Empty, CodeRunnerOptions.UnavailableMessage);
            }
        }
        var page = await _pages.GetAsync(slug, model, cancellationToken);
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
        return model == null ? NotFound() : View(model);
    }
}
