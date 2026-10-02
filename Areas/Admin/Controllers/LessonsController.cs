using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.ViewModels.Admin;
using IntelligentProgrammingPlatform.ViewModels.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[RequestSizeLimit(512 * 1024)]
public class LessonsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<AdminResource> _text;

    // Сабақтарды басқаруға қажет дерекқор мен аудармаларды қабылдайды.
    public LessonsController(ApplicationDbContext db, IStringLocalizer<AdminResource> text)
    {
        _db = db;
        _text = text;
    }

    [HttpGet]
    // Әкімшіге жарияланған және жасырын сабақтарды тақырып бойынша көрсетеді.
    public async Task<IActionResult> Index(int? topicId)
    {
        var lessons = _db.Lessons.AsNoTracking();
        if (topicId.HasValue) lessons = lessons.Where(lesson => lesson.TopicId == topicId);
        return View(new LessonCatalogViewModel
        {
            TopicId = topicId,
            Topics = await _db.Topics.AsNoTracking().OrderBy(topic => topic.Name)
                .Select(topic => new SelectListItem(topic.Name, topic.Id.ToString())).ToListAsync(),
            Lessons = await lessons.OrderBy(lesson => lesson.Topic.Name).ThenBy(lesson => lesson.TopicId)
                .ThenBy(lesson => lesson.Order).ThenBy(lesson => lesson.Id)
                .Select(lesson => new LessonListItemViewModel
                {
                    Id = lesson.Id, Title = lesson.Title, Slug = lesson.Slug, Summary = lesson.Summary,
                    TopicId = lesson.TopicId, TopicName = lesson.Topic.Name, Order = lesson.Order,
                    IsPublished = lesson.IsPublished
                }).ToListAsync()
        });
    }

    [HttpGet]
    // Әкімшіге сабақтың толық мазмұнын қауіпсіз алдын ала көрсетеді.
    public async Task<IActionResult> Details(int id)
    {
        var lesson = await _db.Lessons.AsNoTracking().Include(lesson => lesson.Topic)
            .SingleOrDefaultAsync(lesson => lesson.Id == id);
        return lesson == null ? NotFound() : View(lesson);
    }

    [HttpGet]
    // Жаңа сабақ формасына қолжетімді тақырыптарды жүктейді.
    public async Task<IActionResult> Create()
    {
        var model = new LessonFormViewModel();
        await LoadTopicsAsync(model);
        return View(model);
    }

    [HttpPost]
    // Тексерілген сабаққа сервер уақытын қойып, дерекқорға сақтайды.
    public async Task<IActionResult> Create(LessonFormViewModel model)
    {
        await ValidateAsync(model);
        if (ModelState.IsValid)
        {
            var lesson = new Lesson { CreatedAt = DateTime.UtcNow };
            MapFields(model, lesson);
            _db.Lessons.Add(lesson);
            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = _text["Lesson_Created"].Value;
                return RedirectToAction(nameof(Details), new { lesson.Id });
            }
            catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
            {
                ModelState.AddModelError(string.Empty, _text["Lesson_Constraint"]);
            }
        }
        await LoadTopicsAsync(model);
        return View(model);
    }

    [HttpGet]
    // Сабақтың өзгертуге рұқсат етілген өрістерін формаға көшіреді.
    public async Task<IActionResult> Edit(int id)
    {
        var lesson = await _db.Lessons.AsNoTracking().SingleOrDefaultAsync(lesson => lesson.Id == id);
        if (lesson == null) return NotFound();
        var model = new LessonFormViewModel
        {
            Title = lesson.Title, Slug = lesson.Slug, Summary = lesson.Summary, Content = lesson.Content,
            CodeExample = lesson.CodeExample, CodeLanguage = lesson.CodeLanguage, TopicId = lesson.TopicId,
            Order = lesson.Order, IsPublished = lesson.IsPublished
        };
        await LoadTopicsAsync(model);
        return View(model);
    }

    [HttpPost]
    // Сабақты және жариялау күйін бастапқы жасалу уақытын өзгертпей жаңартады.
    public async Task<IActionResult> Edit(int id, LessonFormViewModel model)
    {
        var lesson = await _db.Lessons.FindAsync(id);
        if (lesson == null) return NotFound();
        await ValidateAsync(model, id);
        if (ModelState.IsValid)
        {
            MapFields(model, lesson);
            try
            {
                await _db.SaveChangesAsync();
                TempData["Success"] = _text["Lesson_Updated"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (DbUpdateConcurrencyException) { return NotFound(); }
            catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
            {
                ModelState.AddModelError(string.Empty, _text["Lesson_Constraint"]);
            }
        }
        await LoadTopicsAsync(model);
        return View(model);
    }

    [HttpGet]
    // Өшіру алдында сабақ атауын көрсетіп, растау формасын ашады.
    public async Task<IActionResult> Delete(int id)
    {
        var lesson = await _db.Lessons.AsNoTracking().SingleOrDefaultAsync(lesson => lesson.Id == id);
        return lesson == null ? NotFound() : View(lesson);
    }

    [HttpPost, ActionName("Delete")]
    // Расталған сабақты ғана өшіріп, дерекқор шектеулерін сақтайды.
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lesson = await _db.Lessons.FindAsync(id);
        if (lesson == null) return NotFound();
        _db.Lessons.Remove(lesson);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, _text["Lesson_Constraint"]);
            return View("Delete", lesson);
        }
        TempData["Success"] = _text["Lesson_Deleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    // Slug, тақырып және тақырып ішіндегі реттің қайталанбауын тексереді.
    private async Task ValidateAsync(LessonFormViewModel model, int? id = null)
    {
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Slug = model.Slug?.Trim() ?? string.Empty;
        model.Summary = model.Summary?.Trim() ?? string.Empty;
        if (await _db.Lessons.AnyAsync(lesson => lesson.Slug == model.Slug && lesson.Id != id))
            ModelState.AddModelError(nameof(model.Slug), _text["Lesson_DuplicateSlug"]);
        if (await _db.Lessons.AnyAsync(lesson => lesson.TopicId == model.TopicId && lesson.Order == model.Order && lesson.Id != id))
            ModelState.AddModelError(nameof(model.Order), _text["Lesson_DuplicateOrder"]);
        if (!await _db.Topics.AnyAsync(topic => topic.Id == model.TopicId))
            ModelState.AddModelError(nameof(model.TopicId), _text["Validation_Topic"]);
    }

    // Сабақ формасының тақырып таңдау тізімін дайындайды.
    private async Task LoadTopicsAsync(LessonFormViewModel model) =>
        model.Topics = await _db.Topics.AsNoTracking().OrderBy(topic => topic.Name)
            .Select(topic => new SelectListItem(topic.Name, topic.Id.ToString())).ToListAsync();

    // Тек формада рұқсат етілген өрістерді сабақ жазбасына көшіреді.
    private static void MapFields(LessonFormViewModel model, Lesson lesson)
    {
        lesson.Title = model.Title;
        lesson.Slug = model.Slug;
        lesson.Summary = model.Summary;
        lesson.Content = model.Content;
        lesson.CodeExample = model.CodeExample;
        lesson.CodeLanguage = model.CodeLanguage;
        lesson.TopicId = model.TopicId;
        lesson.Order = model.Order;
        lesson.IsPublished = model.IsPublished;
    }
}
