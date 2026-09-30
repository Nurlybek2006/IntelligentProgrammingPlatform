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
public class TestCasesController : Controller
{
    private readonly ApplicationDbContext _db;
    // Контроллерге дерекқор және қажетті қызметтерді береді.
    public TestCasesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    // Әкімшіге тест жазбаларының тізімін көрсетеді.
    public async Task<IActionResult> Index(int? taskId)
    {
        if (taskId.HasValue && !await _db.ProgrammingTasks.AnyAsync(task => task.Id == taskId))
            return NotFound();
        ViewBag.TaskId = taskId;
        ViewBag.Tasks = new SelectList(await _db.ProgrammingTasks.AsNoTracking()
            .OrderBy(task => task.Title).ToListAsync(), "Id", "Title", taskId);
        var query = _db.TestCases.AsNoTracking().Include(test => test.ProgrammingTask).AsQueryable();
        if (taskId.HasValue) query = query.Where(test => test.ProgrammingTaskId == taskId);
        return View(await query.OrderBy(test => test.ProgrammingTask.Title).ThenBy(test => test.Order).ToListAsync());
    }

    [HttpGet]
    // Жаңа тест енгізу формасын дайындайды.
    public async Task<IActionResult> Create(int taskId)
    {
        if (!await LoadTaskAsync(taskId)) return NotFound();
        var lastOrder = await _db.TestCases.Where(test => test.ProgrammingTaskId == taskId)
            .MaxAsync(test => (int?)test.Order) ?? -1;
        return View(new TestCaseFormViewModel { ProgrammingTaskId = taskId, Order = lastOrder + 1 });
    }

    [HttpPost]
    // Тексерілген жаңа тест жазбасын сақтайды.
    public async Task<IActionResult> Create(TestCaseFormViewModel model)
    {
        if (!await LoadTaskAsync(model.ProgrammingTaskId)) return NotFound();
        await ValidateOrderAsync(model);
        if (!ModelState.IsValid) return View(model);
        var test = new TestCase { ProgrammingTaskId = model.ProgrammingTaskId };
        MapFields(model, test);
        _db.TestCases.Add(test);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, "This order is already used or the parent task no longer exists.");
            return View(model);
        }
        TempData["Success"] = "Test case created.";
        return RedirectToAction(nameof(Index), new { taskId = model.ProgrammingTaskId });
    }

    [HttpGet]
    // Бар тест жазбасын өзгерту формасына жүктейді.
    public async Task<IActionResult> Edit(int id)
    {
        var test = await _db.TestCases.FindAsync(id);
        if (test == null) return NotFound();
        await LoadTaskAsync(test.ProgrammingTaskId);
        return View(new TestCaseFormViewModel
        {
            ProgrammingTaskId = test.ProgrammingTaskId, Input = test.Input,
            ExpectedOutput = test.ExpectedOutput, IsHidden = test.IsHidden, Order = test.Order
        });
    }

    [HttpPost]
    // Рұқсат етілген тест өрістерін тексеріп жаңартады.
    public async Task<IActionResult> Edit(int id, TestCaseFormViewModel model)
    {
        var test = await _db.TestCases.FindAsync(id);
        if (test == null) return NotFound();
        if (test.ProgrammingTaskId != model.ProgrammingTaskId) return BadRequest();
        await LoadTaskAsync(test.ProgrammingTaskId);
        await ValidateOrderAsync(model, id);
        if (!ModelState.IsValid) return View(model);
        MapFields(model, test);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, "This order is already used or the parent task no longer exists.");
            return View(model);
        }
        TempData["Success"] = "Test case updated.";
        return RedirectToAction(nameof(Index), new { taskId = test.ProgrammingTaskId });
    }

    [HttpGet]
    // Тест жазбасын өшіруді растау бетін көрсетеді.
    public async Task<IActionResult> Delete(int id)
    {
        var test = await _db.TestCases.AsNoTracking().Include(test => test.ProgrammingTask)
            .SingleOrDefaultAsync(test => test.Id == id);
        return test == null ? NotFound() : View(test);
    }

    [HttpPost, ActionName("Delete")]
    // Тәуелді тарих жоқ болса ғана тест жазбасын өшіреді.
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var test = await _db.TestCases.Include(test => test.ProgrammingTask).SingleOrDefaultAsync(test => test.Id == id);
        if (test == null) return NotFound();
        if (await _db.ExecutionResults.AnyAsync(result => result.TestCaseId == id))
        {
            ModelState.AddModelError(string.Empty, "This test case has execution results and cannot be deleted.");
            return View("Delete", test);
        }
        _db.TestCases.Remove(test);
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsConstraintViolation(exception))
        {
            ModelState.AddModelError(string.Empty, "This test case now has execution results and cannot be deleted.");
            return View("Delete", test);
        }
        TempData["Success"] = "Test case deleted.";
        return RedirectToAction(nameof(Index), new { taskId = test.ProgrammingTaskId });
    }

    // Тест тиесілі есептің бар екенін тексеріп, атауын жүктейді.
    private async Task<bool> LoadTaskAsync(int taskId)
    {
        var task = await _db.ProgrammingTasks.AsNoTracking().SingleOrDefaultAsync(task => task.Id == taskId);
        ViewBag.TaskTitle = task?.Title;
        return task != null;
    }

    // Есеп ішіндегі тест ретінің қайталанбауын тексереді.
    private async Task ValidateOrderAsync(TestCaseFormViewModel model, int? id = null)
    {
        if (await _db.TestCases.AnyAsync(test => test.ProgrammingTaskId == model.ProgrammingTaskId
                && test.Order == model.Order && test.Id != id))
            ModelState.AddModelError(nameof(model.Order), "This task already has a test case with this order.");
    }

    // ViewModel-дегі рұқсат етілген өрістерді тест entity-іне көшіреді.
    private static void MapFields(TestCaseFormViewModel model, TestCase test)
    {
        test.Input = model.Input;
        test.ExpectedOutput = model.ExpectedOutput;
        test.IsHidden = model.IsHidden;
        test.Order = model.Order;
    }
}
