using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.AI;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class AttemptJourneyService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AttemptJourneyService> _logger;

    // Тек оқылатын journey сұрауларына дерекқор контекстін береді.
    public AttemptJourneyService(ApplicationDbContext db, ILogger<AttemptJourneyService> logger)
    {
        _db = db;
        _logger = logger;
    }

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
        var rows = await query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip(offset).Take(compact ? 5 : 20)
            .Select(item => new
            {
                item.Id, item.Status, item.CreatedAt, item.PassedTests, item.TotalTests, item.ExecutionTimeMs,
                RuntimeName = item.Runtime.Name,
                PreviousId = query.Where(previous => previous.RuntimeId == item.RuntimeId
                        && (previous.CreatedAt < item.CreatedAt || (previous.CreatedAt == item.CreatedAt && previous.Id < item.Id)))
                    .OrderByDescending(previous => previous.CreatedAt).ThenByDescending(previous => previous.Id)
                    .Select(previous => (long?)previous.Id).FirstOrDefault(),
                FeedbackId = item.AiFeedback != null && item.AiFeedback.UserId == userId ? (long?)item.AiFeedback.Id : null,
                HintsJson = item.AiFeedback != null && item.AiFeedback.UserId == userId ? item.AiFeedback.HintsJson : null,
                Revealed = item.AiFeedback != null && item.AiFeedback.UserId == userId ? item.AiFeedback.RevealedHintCount : 0
            }).ToListAsync(cancellationToken);
        // Legacy JSON серверде ғана оқылады; Razor моделіне кеңес мәтіні ешқашан берілмейді.
        var attempts = rows.Select(item =>
        {
            var count = item.FeedbackId.HasValue ? StoredHintReader.Read(item.HintsJson, _logger, item.FeedbackId.Value).Length : 0;
            return new JourneyAttemptViewModel
            {
                SubmissionId = item.Id, Status = item.Status, CreatedAt = item.CreatedAt,
                RuntimeName = item.RuntimeName, PreviousSubmissionId = item.PreviousId,
                PassedTests = item.PassedTests, TotalTests = item.TotalTests, ExecutionTimeMs = item.ExecutionTimeMs,
                HasAiFeedback = item.FeedbackId.HasValue, AvailableHintCount = count,
                RevealedHintCount = Math.Clamp(item.Revealed, 0, count)
            };
        }).ToList();
        for (var index = 0; index < attempts.Count; index++)
        {
            attempts[index].Number = offset + index + 1;
        }
        return new AttemptJourneyViewModel
        {
            TaskTitle = task.Title, TaskSlug = task.Slug, IsPublished = task.IsPublished,
            TotalAttempts = total, Page = page, PageCount = pages, IsCompact = compact, Attempts = attempts
        };
    }
}
