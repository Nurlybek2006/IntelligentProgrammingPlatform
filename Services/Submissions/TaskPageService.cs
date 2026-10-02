using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using IntelligentProgrammingPlatform.ViewModels.Tasks;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class TaskPageService
{
    private readonly ApplicationDbContext _db;
    private readonly AttemptJourneyService _journeys;

    // Есеп бетін құратын қызметке дерекқор контекстін береді.
    public TaskPageService(ApplicationDbContext db, AttemptJourneyService journeys)
    {
        _db = db;
        _journeys = journeys;
    }

    // Жарияланған есептің тек ашық мысалдарын және рұқсат етілген runtime тізімін дайындайды.
    public async Task<TaskDetailsViewModel?> GetAsync(string slug, SubmitViewModel? form,
        CancellationToken cancellationToken, string? userId = null)
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

        model.Submission = form ?? new SubmitViewModel { ProgrammingTaskId = model.Id };
        var runtimes = await _db.Runtimes.AsNoTracking()
            .Where(runtime => runtime.IsEnabled && RunnerLanguage.Keys.Contains(runtime.LanguageKey))
            .OrderBy(runtime => runtime.Name)
            .Select(runtime => new { runtime.Id, runtime.Name, runtime.LanguageKey })
            .ToListAsync(cancellationToken);
        model.Submission.Runtimes = runtimes
            .Select(runtime => new { runtime.Id, runtime.Name, Language = RunnerLanguage.Find(runtime.LanguageKey) })
            .Where(runtime => runtime.Language != null)
            .Select(runtime => new RuntimeOptionViewModel
            {
                Text = runtime.Name, Value = runtime.Id.ToString(), LanguageKey = runtime.Language!.Key,
                StarterCode = runtime.Language.StarterCode
            }).ToList();
        if (form == null && model.Submission.Runtimes.Count > 0)
        {
            model.Submission.RuntimeId = int.Parse(model.Submission.Runtimes[0].Value);
            model.Submission.SourceCode = model.Submission.Runtimes[0].StarterCode;
        }
        if (userId != null)
            model.Journey = await _journeys.GetAsync(slug, userId, cancellationToken, compact: true);
        return model;
    }
}
