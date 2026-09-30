using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = RoleNames.Admin)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class TopicsController : Controller
{
    private readonly ApplicationDbContext _db;
    public TopicsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(await _db.Topics.AsNoTracking().OrderBy(topic => topic.Name).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new TopicFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(TopicFormViewModel model)
    {
        await ValidateNameAsync(model);
        if (!ModelState.IsValid)
            return View(model);

        _db.Topics.Add(new Topic { Name = model.Name, Description = model.Description });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(nameof(model.Name), "A topic with this name already exists.");
            return View(model);
        }
        TempData["Success"] = "Topic created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var topic = await _db.Topics.FindAsync(id);
        return topic == null ? NotFound() : View(new TopicFormViewModel
        {
            Name = topic.Name, Description = topic.Description
        });
    }

    [HttpPost]
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
            ModelState.AddModelError(nameof(model.Name), "A topic with this name already exists.");
            return View(model);
        }
        TempData["Success"] = "Topic updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var topic = await _db.Topics.AsNoTracking().SingleOrDefaultAsync(topic => topic.Id == id);
        return topic == null ? NotFound() : View(topic);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var topic = await _db.Topics.FindAsync(id);
        if (topic == null) return NotFound();
        if (await _db.ProgrammingTasks.AnyAsync(task => task.TopicId == id))
        {
            ModelState.AddModelError(string.Empty, "This topic contains programming tasks. Move or delete them before deleting the topic.");
            return View("Delete", topic);
        }
        _db.Topics.Remove(topic);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, "This topic is now used by a task and cannot be deleted.");
            return View("Delete", topic);
        }
        TempData["Success"] = "Topic deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateNameAsync(TopicFormViewModel model, int? id = null)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        if (await _db.Topics.AnyAsync(topic => topic.Name == model.Name && topic.Id != id))
            ModelState.AddModelError(nameof(model.Name), "A topic with this name already exists.");
    }
}
