using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.ViewModels.Progress;

public sealed class LearningInsightsViewModel
{
    public List<TopicStrengthViewModel> Topics { get; set; } = new();
    public List<LearningPatternViewModel> ErrorPatterns { get; set; } = new();
    public List<LearningPatternViewModel> AiPatterns { get; set; } = new();
    public PracticeNextViewModel? PracticeNext { get; set; }
    public int PublishedTasks => Topics.Sum(topic => topic.PublishedTasks);
    public bool AllPublishedSolved => PublishedTasks > 0 && Topics.Sum(topic => topic.SolvedTasks) == PublishedTasks;
}

public sealed class TopicStrengthViewModel
{
    public int TopicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PublishedTasks { get; set; }
    public int SolvedTasks { get; set; }
    public int CompletedSubmissions { get; set; }
    public int AcceptedSubmissions { get; set; }
    public int CompilationErrors { get; set; }
    public int WrongAnswers { get; set; }
    public int RuntimeErrors { get; set; }
    public int TimeLimitErrors { get; set; }
    public int MemoryLimitErrors { get; set; }
    public decimal CompletionRate => PublishedTasks == 0 ? 0 : (decimal)SolvedTasks / PublishedTasks;
    public decimal SubmissionSuccessRate => CompletedSubmissions == 0 ? 0 : (decimal)AcceptedSubmissions / CompletedSubmissions;
    public decimal StrengthScore => Math.Clamp((CompletionRate * 0.70m + SubmissionSuccessRate * 0.30m) * 100, 0, 100);
    public string Label => CompletedSubmissions == 0 ? "Not started" : CompletedSubmissions < 3 ? "Exploring"
        : StrengthScore >= 75 ? "Strong" : StrengthScore >= 45 ? "Developing" : "Needs practice";
    public string MostFrequentIssue => new[]
    {
        new LearningPatternViewModel("Compilation", CompilationErrors), new("Wrong Answer / Logic", WrongAnswers),
        new("Runtime", RuntimeErrors), new("Time Limit", TimeLimitErrors), new("Memory Limit", MemoryLimitErrors)
    }.Where(item => item.Count > 0).OrderByDescending(item => item.Count).FirstOrDefault()?.Name ?? "No recorded errors";
}

public sealed record LearningPatternViewModel(string Name, int Count);

public sealed class PracticeNextViewModel
{
    public int TaskId { get; set; }
    public int TopicId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public Difficulty Difficulty { get; set; }
    public string Reason { get; set; } = string.Empty;
}
