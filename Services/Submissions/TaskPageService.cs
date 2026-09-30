using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using IntelligentProgrammingPlatform.ViewModels.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class TaskPageService
{
    private readonly ApplicationDbContext _db;

    // Есеп бетін құратын қызметке дерекқор контекстін береді.
    public TaskPageService(ApplicationDbContext db) => _db = db;

    // Жарияланған есептің тек ашық мысалдарын және рұқсат етілген runtime тізімін дайындайды.
    public async Task<TaskDetailsViewModel?> GetAsync(string slug, SubmitViewModel? form,
        CancellationToken cancellationToken)
    {
        var model = await _db.ProgrammingTasks.AsNoTracking()
            .Where(task => task.IsPublished && task.Slug == slug)
            .Select(task => new TaskDetailsViewModel
            {
                Id = task.Id, Title = task.Title, TopicName = task.Topic.Name, Difficulty = task.Difficulty,
                Description = task.Description, TimeLimitMs = task.TimeLimitMs, MemoryLimitMb = task.MemoryLimitMb,
                Examples = task.TestCases.Where(test => !test.IsHidden).OrderBy(test => test.Order)
                    .Select(test => new TaskExampleViewModel
                    {
                        Input = test.Input, ExpectedOutput = test.ExpectedOutput
                    }).ToList()
            }).SingleOrDefaultAsync(cancellationToken);
        if (model == null) return null;

        model.Submission = form ?? new SubmitViewModel { ProgrammingTaskId = model.Id, SourceCode = SubmitViewModel.StarterCode };
        model.Submission.Runtimes = await _db.Runtimes.AsNoTracking()
            .Where(runtime => runtime.IsEnabled && runtime.LanguageKey == CodeRunnerOptions.LanguageKey)
            .OrderBy(runtime => runtime.Name)
            .Select(runtime => new SelectListItem(runtime.Name, runtime.Id.ToString()))
            .ToListAsync(cancellationToken);
        if (form == null && model.Submission.Runtimes.Count > 0)
            model.Submission.RuntimeId = int.Parse(model.Submission.Runtimes[0].Value);
        return model;
    }
}
