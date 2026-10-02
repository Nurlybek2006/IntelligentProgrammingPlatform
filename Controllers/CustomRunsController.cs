using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.Services.Submissions;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CustomRunsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CustomRunService _runs;
    private readonly IStringLocalizer<SharedResource> _text;

    // Тек жарияланған task/runtime оқуға және уақытша орындауға қажет қызметтерді алады.
    public CustomRunsController(ApplicationDbContext db, CustomRunService runs, IStringLocalizer<SharedResource> text)
    {
        _db = db;
        _runs = runs;
        _text = text;
    }

    [HttpPost, RequestSizeLimit(512 * 1024)]
    // CSRF қорғалған Run сұрауын тексеріп, дерекқорға жазбай JSON нәтижесін қайтарады.
    public async Task<IActionResult> Run(CustomRunViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = _text["Run_Invalid"].Value });
        var task = await _db.ProgrammingTasks.AsNoTracking()
            .Where(task => task.Id == model.ProgrammingTaskId && task.IsPublished)
            .Select(task => new { task.TimeLimitMs, task.MemoryLimitMb }).SingleOrDefaultAsync(cancellationToken);
        if (task == null) return NotFound(new { error = _text["Validation_PublishedTask"].Value });
        var languageKey = await _db.Runtimes.AsNoTracking().Where(runtime => runtime.Id == model.RuntimeId && runtime.IsEnabled)
            .Select(runtime => runtime.LanguageKey).SingleOrDefaultAsync(cancellationToken);
        var language = RunnerLanguage.Find(languageKey);
        if (language == null)
            return BadRequest(new { error = _text["Validation_Runtime"].Value });
        var result = await _runs.RunAsync(model.SourceCode, model.CustomInput ?? string.Empty,
            task.TimeLimitMs, task.MemoryLimitMb, cancellationToken, language);
        return Json(new CustomRunResult
        {
            Status = result.Status, Output = result.Output, CompilerOutput = result.CompilerOutput,
            ExitCode = result.ExitCode, ExecutionTimeMs = result.ExecutionTimeMs,
            CompileSucceeded = result.CompileSucceeded, Error = LocalizeError(result)
        });
    }

    // Runner жасаған хабардың басын ғана аударады, студенттің STDERR мәтінін өзгертпейді.
    private string? LocalizeError(CustomRunResult result)
    {
        var (prefix, key) = result.Status switch
        {
            CustomRunStatus.CompilationError => ("Compilation failed.", "Run_CompilationFailed"),
            CustomRunStatus.InternalError => (CodeRunnerOptions.UnavailableMessage, "Runner_Unavailable"),
            CustomRunStatus.TimeLimitExceeded => ("Time limit exceeded.", "Run_TimeLimit"),
            CustomRunStatus.MemoryLimitExceeded => ("Memory limit exceeded.", "Run_MemoryLimit"),
            CustomRunStatus.RuntimeError when result.Error?.StartsWith("Output limit exceeded.", StringComparison.Ordinal) == true
                => ("Output limit exceeded.", "Run_OutputLimit"),
            CustomRunStatus.RuntimeError => ("Runtime error.", "Run_RuntimeError"),
            _ => (string.Empty, string.Empty)
        };
        return prefix.Length > 0 && result.Error != null
            && (result.Error == prefix || result.Error.StartsWith(prefix + "\n", StringComparison.Ordinal))
            ? _text[key].Value + result.Error[prefix.Length..] : result.Error;
    }
}
