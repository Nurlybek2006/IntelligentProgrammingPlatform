using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class TopicsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer<AdminResource> _text;
    // Контроллерге дерекқор және қажетті қызметтерді береді.
    public TopicsController(ApplicationDbContext db, IStringLocalizer<AdminResource> text)
    {
        _db = db;
        _text = text;
    }

    [HttpGet]
    // Әкімшіге тақырып жазбаларының тізімін көрсетеді.
    public async Task<IActionResult> Index() =>
        View(await _db.Topics.AsNoTracking().OrderBy(topic => topic.Name).ToListAsync());

    [HttpGet]
    // Жаңа тақырып енгізу формасын дайындайды.
    public IActionResult Create() => View(new TopicFormViewModel());

    [HttpPost]
    // Тексерілген жаңа тақырып жазбасын сақтайды.
    public async Task<IActionResult> Create(TopicFormViewModel model)
    {
        await ValidateNameAsync(model);
        if (!ModelState.IsValid)
            return View(model);

        _db.Topics.Add(new Topic { Name = model.Name, Description = model.Description });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(nameof(model.Name), _text["Topic_Duplicate"]);
            return View(model);
        }
        TempData["Success"] = _text["Topic_Created"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    // Бар тақырып жазбасын өзгерту формасына жүктейді.
    public async Task<IActionResult> Edit(int id)
    {
        var topic = await _db.Topics.FindAsync(id);
        return topic == null ? NotFound() : View(new TopicFormViewModel
        {
            Name = topic.Name, Description = topic.Description
        });
    }

    [HttpPost]
    // Рұқсат етілген тақырып өрістерін тексеріп жаңартады.
    public async Task<IActionResult> Edit(int id, TopicFormViewModel model)
    {
        var topic = await _db.Topics.FindAsync(id);
        if (topic == null) return NotFound();
        await ValidateNameAsync(model, id);
        if (!ModelState.IsValid) return View(model);

        topic.Name = model.Name;
        topic.Description = model.Description;
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(nameof(model.Name), _text["Topic_Duplicate"]);
            return View(model);
        }
        TempData["Success"] = _text["Topic_Updated"].Value;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    // Тақырып жазбасын өшіруді растау бетін көрсетеді.
    public async Task<IActionResult> Delete(int id)
    {
        var topic = await _db.Topics.AsNoTracking().SingleOrDefaultAsync(topic => topic.Id == id);
        return topic == null ? NotFound() : View(topic);
    }

    [HttpPost, ActionName("Delete")]
    // Тәуелді тарих жоқ болса ғана тақырып жазбасын өшіреді.
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var topic = await _db.Topics.FindAsync(id);
        if (topic == null) return NotFound();
        if (await _db.ProgrammingTasks.AnyAsync(task => task.TopicId == id))
        {
            ModelState.AddModelError(string.Empty, _text["Topic_HasTasks"]);
            return View("Delete", topic);
        }
        if (await _db.Lessons.AnyAsync(lesson => lesson.TopicId == id))
        {
            ModelState.AddModelError(string.Empty, _text["Topic_HasLessons"]);
            return View("Delete", topic);
        }
        _db.Topics.Remove(topic);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, _text["Topic_InUse"]);
            return View("Delete", topic);
        }
        TempData["Success"] = _text["Topic_Deleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    // Тақырып атауын қалыпқа келтіріп, қайталануын тексереді.
    private async Task ValidateNameAsync(TopicFormViewModel model, int? id = null)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        if (await _db.Topics.AnyAsync(topic => topic.Name == model.Name && topic.Id != id))
            ModelState.AddModelError(nameof(model.Name), _text["Topic_Duplicate"]);
    }
}
