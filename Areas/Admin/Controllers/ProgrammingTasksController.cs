using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ProgrammingTasksController : Controller
{
    private readonly ApplicationDbContext _db;
    // Контроллерге дерекқор және қажетті қызметтерді береді.
    public ProgrammingTasksController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    // Әкімшіге есеп жазбаларының тізімін көрсетеді.
    public async Task<IActionResult> Index() => View(await _db.ProgrammingTasks.AsNoTracking()
        .Include(task => task.Topic).OrderBy(task => task.Title).ToListAsync());

    [HttpGet]
    // Әкімшіге есептің толық деректерін және тесттерін көрсетеді.
    public async Task<IActionResult> Details(int id)
    {
        var task = await _db.ProgrammingTasks.AsNoTracking().Include(task => task.Topic)
            .Include(task => task.TestCases.OrderBy(test => test.Order))
            .SingleOrDefaultAsync(task => task.Id == id);
        return task == null ? NotFound() : View(task);
    }

    [HttpGet]
    // Жаңа есеп енгізу формасын дайындайды.
    public async Task<IActionResult> Create()
    {
        var model = new ProgrammingTaskFormViewModel();
        await LoadTopicsAsync(model);
        return View(model);
    }

    [HttpPost]
    // Тексерілген жаңа есеп жазбасын сақтайды.
    public async Task<IActionResult> Create(ProgrammingTaskFormViewModel model)
    {
        await ValidateAsync(model);
        if (!ModelState.IsValid)
        {
            await LoadTopicsAsync(model);
            return View(model);
        }
        var task = new ProgrammingTask { CreatedAt = DateTime.UtcNow };
        MapFields(model, task);
        _db.ProgrammingTasks.Add(task);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, "The slug is already in use or the selected topic no longer exists.");
            await LoadTopicsAsync(model);
            return View(model);
        }
        TempData["Success"] = "Programming task created.";
        return RedirectToAction(nameof(Details), new { task.Id });
    }

    [HttpGet]
    // Бар есеп жазбасын өзгерту формасына жүктейді.
    public async Task<IActionResult> Edit(int id)
    {
        var task = await _db.ProgrammingTasks.FindAsync(id);
        if (task == null) return NotFound();
        var model = new ProgrammingTaskFormViewModel
        {
            Title = task.Title, Slug = task.Slug, Description = task.Description,
            Difficulty = task.Difficulty, TopicId = task.TopicId,
            TimeLimitMs = task.TimeLimitMs, MemoryLimitMb = task.MemoryLimitMb, IsPublished = task.IsPublished
        };
        await LoadTopicsAsync(model);
        return View(model);
    }

    [HttpPost]
    // Рұқсат етілген есеп өрістерін тексеріп жаңартады.
    public async Task<IActionResult> Edit(int id, ProgrammingTaskFormViewModel model)
    {
        var task = await _db.ProgrammingTasks.FindAsync(id);
        if (task == null) return NotFound();
        await ValidateAsync(model, id);
        if (!ModelState.IsValid)
        {
            await LoadTopicsAsync(model);
            return View(model);
        }
        MapFields(model, task);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, "The slug is already in use or the selected topic no longer exists.");
            await LoadTopicsAsync(model);
            return View(model);
        }
        TempData["Success"] = "Programming task updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    // Есеп жазбасын өшіруді растау бетін көрсетеді.
    public async Task<IActionResult> Delete(int id)
    {
        var task = await _db.ProgrammingTasks.AsNoTracking().SingleOrDefaultAsync(task => task.Id == id);
        return task == null ? NotFound() : View(task);
    }

    [HttpPost, ActionName("Delete")]
    // Тәуелді тарих жоқ болса ғана есеп жазбасын өшіреді.
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await _db.ProgrammingTasks.FindAsync(id);
        if (task == null) return NotFound();
        if (await _db.Submissions.AnyAsync(submission => submission.ProgrammingTaskId == id))
            ModelState.AddModelError(string.Empty, "This task has submissions and cannot be deleted. Unpublish it to remove it from the catalog.");
        else if (await _db.TestCases.AnyAsync(test => test.ProgrammingTaskId == id))
            ModelState.AddModelError(string.Empty, "Delete this task's test cases first. Test cases with saved results cannot be deleted.");

        if (!ModelState.IsValid) return View("Delete", task);

        _db.ProgrammingTasks.Remove(task);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, "This task now has dependent records and cannot be deleted.");
            return View("Delete", task);
        }
        TempData["Success"] = "Programming task deleted.";
        return RedirectToAction(nameof(Index));
    }

    // Slug бірегейлігін және тақырыптың бар екенін тексереді.
    private async Task ValidateAsync(ProgrammingTaskFormViewModel model, int? id = null)
    {
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Slug = model.Slug?.Trim().ToLowerInvariant() ?? string.Empty;
        if (await _db.ProgrammingTasks.AnyAsync(task => task.Slug == model.Slug && task.Id != id))
            ModelState.AddModelError(nameof(model.Slug), "A task with this slug already exists.");
        if (!await _db.Topics.AnyAsync(topic => topic.Id == model.TopicId))
            ModelState.AddModelError(nameof(model.TopicId), "Select an existing topic.");
    }

    // Есеп формасына арналған тақырып dropdown тізімін дайындайды.
    private async Task LoadTopicsAsync(ProgrammingTaskFormViewModel model) =>
        model.Topics = await _db.Topics.AsNoTracking().OrderBy(topic => topic.Name)
            .Select(topic => new SelectListItem(topic.Name, topic.Id.ToString())).ToListAsync();

    // ViewModel-дегі рұқсат етілген өрістерді есеп entity-іне көшіреді.
    private static void MapFields(ProgrammingTaskFormViewModel model, ProgrammingTask task)
    {
        task.Title = model.Title;
        task.Slug = model.Slug;
        task.Description = model.Description;
        task.Difficulty = model.Difficulty;
        task.TopicId = model.TopicId;
        task.TimeLimitMs = model.TimeLimitMs;
        task.MemoryLimitMb = model.MemoryLimitMb;
        task.IsPublished = model.IsPublished;
    }
}
