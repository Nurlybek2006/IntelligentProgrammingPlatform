using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Progress;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Progress;

public sealed class LearningInsightsService
{
    private readonly ApplicationDbContext _db;

    // Жеке оқу картасын тек SQL тарихынан есептейтін контексті алады.
    public LearningInsightsService(ApplicationDbContext db) => _db = db;

    // Жарияланған есептердің ресми нәтижелерінен төрт сұраумен жеке оқу картасын құрады.
    public async Task<LearningInsightsViewModel> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var topics = await _db.Topics.AsNoTracking().OrderBy(topic => topic.Name).ThenBy(topic => topic.Id)
            .Select(topic => new TopicStrengthViewModel
            {
                TopicId = topic.Id, Name = topic.Name,
                PublishedTasks = topic.ProgrammingTasks.Count(task => task.IsPublished)
            }).ToListAsync(cancellationToken);
        var completed = _db.Submissions.AsNoTracking().Where(item => item.UserId == userId
            && item.ProgrammingTask.IsPublished && item.Status >= SubmissionStatus.Accepted
            && item.Status <= SubmissionStatus.MemoryLimitExceeded);
        var counts = await completed.GroupBy(item => item.ProgrammingTask.TopicId).Select(group => new
        {
            TopicId = group.Key, Completed = group.Count(),
            Solved = group.Where(item => item.Status == SubmissionStatus.Accepted).Select(item => item.ProgrammingTaskId).Distinct().Count(),
            Accepted = group.Count(item => item.Status == SubmissionStatus.Accepted),
            Compilation = group.Count(item => item.Status == SubmissionStatus.CompilationError),
            Wrong = group.Count(item => item.Status == SubmissionStatus.WrongAnswer),
            Runtime = group.Count(item => item.Status == SubmissionStatus.RuntimeError),
            Time = group.Count(item => item.Status == SubmissionStatus.TimeLimitExceeded),
            Memory = group.Count(item => item.Status == SubmissionStatus.MemoryLimitExceeded)
        }).ToDictionaryAsync(item => item.TopicId, cancellationToken);
        foreach (var topic in topics)
        {
            if (!counts.TryGetValue(topic.TopicId, out var count)) continue;
            topic.SolvedTasks = count.Solved; topic.CompletedSubmissions = count.Completed;
            topic.AcceptedSubmissions = count.Accepted; topic.CompilationErrors = count.Compilation;
            topic.WrongAnswers = count.Wrong; topic.RuntimeErrors = count.Runtime;
            topic.TimeLimitErrors = count.Time; topic.MemoryLimitErrors = count.Memory;
        }
        var analyzed = await _db.AiFeedbacks.AsNoTracking().Where(item => item.UserId == userId
                && item.Submission.UserId == userId && item.Submission.ProgrammingTask.IsPublished
                && item.Submission.Status >= SubmissionStatus.Accepted && item.Submission.Status <= SubmissionStatus.MemoryLimitExceeded)
            .GroupBy(item => item.ErrorCategory).Select(group => new { Category = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var categories = new[] { "Compilation", "Logic", "Runtime", "TimeLimit", "Memory", "OutputFormat", "Unknown" };
        var aiPatterns = analyzed.GroupBy(item => categories.Contains(item.Category) ? item.Category : "Unknown")
            .Select(group => new LearningPatternViewModel(group.Key, group.Sum(item => item.Count)))
            .OrderByDescending(item => item.Count).ThenBy(item => item.Name, StringComparer.Ordinal).ToList();
        // SQL әр тақырыптан тек ең оңай шешілмеген бір есептің метадерегін алады.
        var candidates = await _db.Topics.AsNoTracking().Select(topic => topic.ProgrammingTasks
            .Where(task => task.IsPublished && !task.Submissions.Any(item => item.UserId == userId && item.Status == SubmissionStatus.Accepted))
            .OrderBy(task => task.Difficulty).ThenBy(task => task.Id)
            .Select(task => new PracticeNextViewModel
            {
                TaskId = task.Id, TopicId = task.TopicId, Title = task.Title, Slug = task.Slug,
                Difficulty = task.Difficulty, TopicName = topic.Name
            }).FirstOrDefault()).ToListAsync(cancellationToken);
        var eligible = candidates.Where(item => item != null).ToDictionary(item => item!.TopicId, item => item!);
        var nextTopic = topics.Where(topic => eligible.ContainsKey(topic.TopicId))
            .OrderBy(PracticePriority).ThenBy(topic => topic.StrengthScore).ThenBy(topic => topic.TopicId).FirstOrDefault();
        var next = nextTopic == null ? null : eligible[nextTopic.TopicId];
        if (next != null)
            next.Reason = $"{nextTopic!.Name}: {nextTopic.Label}; {nextTopic.SolvedTasks} of {nextTopic.PublishedTasks} tasks solved. "
                + "Chosen by practice priority, then learning strength; the easiest remaining task in this topic comes first.";
        return new LearningInsightsViewModel
        {
            Topics = topics, AiPatterns = aiPatterns, PracticeNext = next,
            ErrorPatterns = new()
            {
                new("Compilation", topics.Sum(item => item.CompilationErrors)), new("Wrong Answer / Logic", topics.Sum(item => item.WrongAnswers)),
                new("Runtime", topics.Sum(item => item.RuntimeErrors)), new("Time Limit", topics.Sum(item => item.TimeLimitErrors)),
                new("Memory Limit", topics.Sum(item => item.MemoryLimitErrors))
            }
        };
    }

    // Бейтарап оқу белгілерін тұрақты жаттығу басымдығына айналдырады.
    private static int PracticePriority(TopicStrengthViewModel topic) => topic.Label switch
    {
        "Needs practice" => 0, "Developing" => 1, "Exploring" => 2, "Not started" => 3, _ => 4
    };
}
