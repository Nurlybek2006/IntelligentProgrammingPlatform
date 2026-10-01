using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class AttemptJourneyService
{
    private readonly ApplicationDbContext _db;

    // Тек оқылатын journey сұрауларына дерекқор контекстін береді.
    public AttemptJourneyService(ApplicationDbContext db) => _db = db;

    // Иесінің әрекеттерін уақыт пен Id ретімен, source/hidden мәтіндерін алмай беттейді.
    public async Task<AttemptJourneyViewModel?> GetAsync(string slug, string userId,
        CancellationToken cancellationToken, int page = 1, bool compact = false)
    {
        var task = await _db.ProgrammingTasks.AsNoTracking()
            .Where(task => task.Slug == slug && (task.IsPublished || task.Submissions.Any(item => item.UserId == userId)))
            .Select(task => new { task.Id, task.Title, task.Slug, task.IsPublished }).SingleOrDefaultAsync(cancellationToken);
        if (task == null) return null;
        var query = _db.Submissions.AsNoTracking().Where(item => item.UserId == userId && item.ProgrammingTaskId == task.Id);
        var total = await query.CountAsync(cancellationToken);
        var pages = Math.Max(1, (int)Math.Ceiling(total / 20d));
        page = Math.Clamp(page, 1, pages);
        var offset = compact ? Math.Max(0, total - 5) : (page - 1) * 20;
        var predecessorCount = offset > 0 ? 1 : 0;
        var attempts = await query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip(offset - predecessorCount).Take((compact ? 5 : 20) + predecessorCount)
            .Select(item => new JourneyAttemptViewModel
            {
                SubmissionId = item.Id, Status = item.Status, CreatedAt = item.CreatedAt,
                PassedTests = item.PassedTests, TotalTests = item.TotalTests, ExecutionTimeMs = item.ExecutionTimeMs,
                HasAiFeedback = item.AiFeedback != null && item.AiFeedback.UserId == userId
            }).ToListAsync(cancellationToken);
        long? previous = null;
        if (predecessorCount > 0 && attempts.Count > 0)
        {
            previous = attempts[0].SubmissionId;
            attempts.RemoveAt(0);
        }
        for (var index = 0; index < attempts.Count; index++)
        {
            attempts[index].Number = offset + index + 1;
            attempts[index].PreviousSubmissionId = previous;
            previous = attempts[index].SubmissionId;
        }
        return new AttemptJourneyViewModel
        {
            TaskTitle = task.Title, TaskSlug = task.Slug, IsPublished = task.IsPublished,
            TotalAttempts = total, Page = page, PageCount = pages, IsCompact = compact, Attempts = attempts
        };
    }
}
