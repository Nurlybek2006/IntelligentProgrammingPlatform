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
public sealed class CustomRunsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CustomRunService _runs;

    // Тек жарияланған task/runtime оқуға және уақытша орындауға қажет қызметтерді алады.
    public CustomRunsController(ApplicationDbContext db, CustomRunService runs)
    {
        _db = db;
        _runs = runs;
    }

    [HttpPost, RequestSizeLimit(512 * 1024)]
    // CSRF қорғалған Run сұрауын тексеріп, дерекқорға жазбай JSON нәтижесін қайтарады.
    public async Task<IActionResult> Run(CustomRunViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Select a task and runtime, enter source of at most 64 KiB and input of at most 32 KiB (UTF-8)." });
        var task = await _db.ProgrammingTasks.AsNoTracking()
            .Where(task => task.Id == model.ProgrammingTaskId && task.IsPublished)
            .Select(task => new { task.TimeLimitMs, task.MemoryLimitMb }).SingleOrDefaultAsync(cancellationToken);
        if (task == null) return NotFound(new { error = "Select a published programming task." });
        var language = await _db.Runtimes.AsNoTracking().Where(runtime => runtime.Id == model.RuntimeId && runtime.IsEnabled)
            .Select(runtime => runtime.LanguageKey).SingleOrDefaultAsync(cancellationToken);
        if (language != CodeRunnerOptions.LanguageKey)
            return BadRequest(new { error = "Select an enabled C++ runtime." });
        return Json(await _runs.RunAsync(model.SourceCode, model.CustomInput ?? string.Empty,
            task.TimeLimitMs, task.MemoryLimitMb, cancellationToken));
    }
}
