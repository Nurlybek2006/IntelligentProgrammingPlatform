using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.ViewModels.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Controllers;

[AllowAnonymous, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class LessonsController : Controller
{
    private readonly ApplicationDbContext _db;

    // Жарияланған оқу материалдарын оқитын контексті қабылдайды.
    public LessonsController(ApplicationDbContext db) => _db = db;

    [HttpGet("/Lessons")]
    // Тек жарияланған сабақтарды тақырып бойынша сүзіп, ретімен көрсетеді.
    public async Task<IActionResult> Index(int? topicId, CancellationToken cancellationToken)
    {
        var query = _db.Lessons.AsNoTracking().Where(lesson => lesson.IsPublished);
        if (topicId.HasValue) query = query.Where(lesson => lesson.TopicId == topicId);
        return View(new LessonCatalogViewModel
        {
            TopicId = topicId,
            Topics = await _db.Topics.AsNoTracking().Where(topic => topic.Lessons.Any(lesson => lesson.IsPublished))
                .OrderBy(topic => topic.Name).ThenBy(topic => topic.Id)
                .Select(topic => new SelectListItem(topic.Name, topic.Id.ToString())).ToListAsync(cancellationToken),
            Lessons = await query.OrderBy(lesson => lesson.Topic.Name).ThenBy(lesson => lesson.TopicId)
                .ThenBy(lesson => lesson.Order).ThenBy(lesson => lesson.Id)
                .Select(lesson => new LessonListItemViewModel
                {
                    Id = lesson.Id, Title = lesson.Title, Slug = lesson.Slug, Summary = lesson.Summary,
                    TopicId = lesson.TopicId, TopicName = lesson.Topic.Name, Order = lesson.Order
                }).ToListAsync(cancellationToken)
        });
    }

    [HttpGet("/Lessons/{slug}")]
    // Сабақты, жарияланған көршілерін және тек ашық есеп метадеректерін қайтарады.
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken)
    {
        var model = await _db.Lessons.AsNoTracking().Where(lesson => lesson.IsPublished && lesson.Slug == slug)
            .Select(lesson => new LessonDetailsViewModel
            {
                Id = lesson.Id, Title = lesson.Title, Slug = lesson.Slug, Summary = lesson.Summary,
                Content = lesson.Content, CodeExample = lesson.CodeExample, CodeLanguage = lesson.CodeLanguage,
                TopicId = lesson.TopicId, TopicName = lesson.Topic.Name, Order = lesson.Order
            }).SingleOrDefaultAsync(cancellationToken);
        if (model == null) return NotFound();
        var neighbors = _db.Lessons.AsNoTracking().Where(lesson => lesson.IsPublished && lesson.TopicId == model.TopicId);
        model.Previous = await neighbors.Where(lesson => lesson.Order < model.Order || (lesson.Order == model.Order && lesson.Id < model.Id))
            .OrderByDescending(lesson => lesson.Order).ThenByDescending(lesson => lesson.Id)
            .Select(lesson => new LessonLinkViewModel { Title = lesson.Title, Slug = lesson.Slug }).FirstOrDefaultAsync(cancellationToken);
        model.Next = await neighbors.Where(lesson => lesson.Order > model.Order || (lesson.Order == model.Order && lesson.Id > model.Id))
            .OrderBy(lesson => lesson.Order).ThenBy(lesson => lesson.Id)
            .Select(lesson => new LessonLinkViewModel { Title = lesson.Title, Slug = lesson.Slug }).FirstOrDefaultAsync(cancellationToken);
        model.RelatedTasks = await _db.ProgrammingTasks.AsNoTracking().Where(task => task.IsPublished && task.TopicId == model.TopicId)
            .OrderBy(task => task.Difficulty).ThenBy(task => task.Id).Take(3)
            .Select(task => new LessonTaskViewModel { Title = task.Title, Slug = task.Slug, Difficulty = task.Difficulty })
            .ToListAsync(cancellationToken);
        return View(model);
    }
}
