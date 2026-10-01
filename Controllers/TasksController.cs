using System.Security.Claims;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.Submissions;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Controllers;

[AllowAnonymous]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class TasksController : Controller
{
    private readonly ApplicationDbContext _db;

    private readonly TaskPageService _pages;

    // Каталог пен қауіпсіз есеп бетіне қажетті қызметтерді қабылдайды.
    public TasksController(ApplicationDbContext db, TaskPageService pages)
    {
        _db = db;
        _pages = pages;
    }

    [HttpGet("/Tasks")]
    // Жарияланған есептерді сүзгілер бойынша каталогқа шығарады.
    public async Task<IActionResult> Index(string? search, int? topicId, Difficulty? difficulty)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var query = _db.ProgrammingTasks.AsNoTracking().Where(task => task.IsPublished);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(task => task.Title.Contains(search.Trim()));
        if (topicId.HasValue)
            query = query.Where(task => task.TopicId == topicId);
        if (difficulty.HasValue && Enum.IsDefined(difficulty.Value))
            query = query.Where(task => task.Difficulty == difficulty);

        return View(new TaskCatalogViewModel
        {
            Search = search, TopicId = topicId, Difficulty = difficulty,
            Topics = await _db.Topics.AsNoTracking().OrderBy(topic => topic.Name)
                .Select(topic => new SelectListItem(topic.Name, topic.Id.ToString())).ToListAsync(),
            Tasks = await query.OrderBy(task => task.Title).Select(task => new TaskListItemViewModel
            {
                Title = task.Title, Slug = task.Slug, TopicName = task.Topic.Name, Difficulty = task.Difficulty,
                IsSolved = userId != null && task.Submissions.Any(submission => submission.UserId == userId
                    && submission.Status == SubmissionStatus.Accepted)
            }).ToListAsync()
        });
    }

    [HttpGet("/Tasks/{slug}")]
    // Ашық мысалдары бар есеп бетін және код формасын көрсетеді.
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var model = await _pages.GetAsync(slug, null, cancellationToken, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return model == null ? NotFound() : View(model);
    }
}
