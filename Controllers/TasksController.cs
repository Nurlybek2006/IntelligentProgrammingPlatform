using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Controllers;

[AllowAnonymous]
public class TasksController : Controller
{
    private readonly ApplicationDbContext _db;

    public TasksController(ApplicationDbContext db) => _db = db;

    [HttpGet("/Tasks")]
    public async Task<IActionResult> Index(string? search, int? topicId, Difficulty? difficulty)
    {
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
                Title = task.Title, Slug = task.Slug, TopicName = task.Topic.Name, Difficulty = task.Difficulty
            }).ToListAsync()
        });
    }

    [HttpGet("/Tasks/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        // Filter in SQL and project only public fields. Hidden tests never enter the ViewModel.
        var model = await _db.ProgrammingTasks.AsNoTracking()
            .Where(task => task.IsPublished && task.Slug == slug)
            .Select(task => new TaskDetailsViewModel
            {
                Title = task.Title, TopicName = task.Topic.Name, Difficulty = task.Difficulty,
                Description = task.Description, TimeLimitMs = task.TimeLimitMs, MemoryLimitMb = task.MemoryLimitMb,
                Examples = task.TestCases.Where(test => !test.IsHidden).OrderBy(test => test.Order)
                    .Select(test => new TaskExampleViewModel
                    {
                        Input = test.Input, ExpectedOutput = test.ExpectedOutput
                    }).ToList()
            }).SingleOrDefaultAsync();

        return model == null ? NotFound() : View(model);
    }
}
