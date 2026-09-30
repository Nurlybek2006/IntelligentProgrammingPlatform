using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Progress;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Progress;

public sealed class ProgressService
{
    private readonly ApplicationDbContext _db;

    // Статистикаға тек жіберілім тарихын оқитын контекст береді.
    public ProgressService(ApplicationDbContext db) => _db = db;

    // Пайдаланушы нәтижелерін, шешу уақытын және жарияланған есептер прогресін есептейді.
    public async Task<ProgressViewModel> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var submissions = _db.Submissions.AsNoTracking().Where(item => item.UserId == userId);
        var completed = submissions.Where(item => item.Status >= SubmissionStatus.Accepted
            && item.Status <= SubmissionStatus.InternalError);
        var counts = await completed.GroupBy(_ => 1).Select(group => new
        {
            Total = group.Count(), Accepted = group.Count(item => item.Status == SubmissionStatus.Accepted),
            CompilationErrors = group.Count(item => item.Status == SubmissionStatus.CompilationError)
        }).SingleOrDefaultAsync(cancellationToken);
        var solvedTasks = _db.ProgrammingTasks.AsNoTracking().Where(task => submissions.Any(
            item => item.ProgrammingTaskId == task.Id && item.Status == SubmissionStatus.Accepted));
        var solved = await solvedTasks.GroupBy(_ => 1).Select(group => new
        {
            Count = group.Count(),
            Score = group.Sum(task => task.Difficulty == Difficulty.Easy ? 100
                : task.Difficulty == Difficulty.Medium ? 200 : task.Difficulty == Difficulty.Hard ? 300 : 0)
        }).SingleOrDefaultAsync(cancellationToken);
        var model = new ProgressViewModel
        {
            SolvedTasks = solved?.Count ?? 0, Score = solved?.Score ?? 0,
            TotalSubmissions = counts?.Total ?? 0, AcceptedSubmissions = counts?.Accepted ?? 0,
            CompilationErrors = counts?.CompilationErrors ?? 0,
            SuccessRate = counts == null ? 0 : Math.Round(counts.Accepted * 100m / counts.Total, 1)
        };

        var published = _db.ProgrammingTasks.AsNoTracking().Where(task => task.IsPublished);
        var publishedSolved = solvedTasks.Where(task => task.IsPublished);
        var difficultyTotals = await published.GroupBy(task => task.Difficulty)
            .Select(group => new { Difficulty = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var difficultySolved = await publishedSolved.GroupBy(task => task.Difficulty)
            .Select(group => new { Difficulty = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        model.PublishedTasks = difficultyTotals.Sum(item => item.Count);
        model.PublishedSolvedTasks = difficultySolved.Sum(item => item.Count);
        model.Difficulties = Enum.GetValues<Difficulty>().Select(difficulty => new ProgressGroupViewModel
        {
            Name = difficulty.ToString(), Total = difficultyTotals.SingleOrDefault(item => item.Difficulty == difficulty)?.Count ?? 0,
            Solved = difficultySolved.SingleOrDefault(item => item.Difficulty == difficulty)?.Count ?? 0
        }).ToList();
        var topicTotals = await published.GroupBy(task => new { task.TopicId, task.Topic.Name })
            .Select(group => new { group.Key.TopicId, group.Key.Name, Count = group.Count() }).ToListAsync(cancellationToken);
        var topicSolved = await publishedSolved.GroupBy(task => task.TopicId)
            .Select(group => new { TopicId = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        model.Topics = topicTotals.OrderBy(item => item.Name).Select(topic => new ProgressGroupViewModel
        {
            Name = topic.Name, Total = topic.Count,
            Solved = topicSolved.SingleOrDefault(item => item.TopicId == topic.TopicId)?.Count ?? 0
        }).ToList();

        var solveTimes = await submissions.GroupBy(item => item.ProgrammingTaskId).Select(group => new
        {
            First = group.Min(item => item.CreatedAt),
            Accepted = group.Min(item => item.Status == SubmissionStatus.Accepted ? (DateTime?)item.CreatedAt : null)
        }).Where(item => item.Accepted != null).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var validTimes = solveTimes.Where(item => item.First > DateTime.MinValue && item.Accepted >= item.First
                && item.Accepted <= now)
            .Select(item => (item.Accepted!.Value - item.First).TotalSeconds).ToList();
        model.AverageSolveSeconds = validTimes.Count == 0 ? null : validTimes.Average();
        model.Recent = await submissions.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(5).Select(item => new SubmissionListItemViewModel
            {
                Id = item.Id, TaskTitle = item.ProgrammingTask.Title, RuntimeName = item.Runtime.Name,
                Status = item.Status, PassedTests = item.PassedTests, TotalTests = item.TotalTests, CreatedAt = item.CreatedAt
            }).ToListAsync(cancellationToken);
        return model;
    }
}
