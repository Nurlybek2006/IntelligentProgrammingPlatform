using IntelligentProgrammingPlatform.ViewModels.Submissions;

namespace IntelligentProgrammingPlatform.ViewModels.Progress;

public class ProgressViewModel
{
    public int SolvedTasks { get; set; }
    public int PublishedSolvedTasks { get; set; }
    public int PublishedTasks { get; set; }
    public int TotalSubmissions { get; set; }
    public int AcceptedSubmissions { get; set; }
    public int CompilationErrors { get; set; }
    public decimal SuccessRate { get; set; }
    public double? AverageSolveSeconds { get; set; }
    public int Score { get; set; }
    public List<ProgressGroupViewModel> Difficulties { get; set; } = new();
    public List<ProgressGroupViewModel> Topics { get; set; } = new();
    public List<SubmissionListItemViewModel> Recent { get; set; } = new();
    public LearningInsightsViewModel LearningMap { get; set; } = new();
}

public class ProgressGroupViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Solved { get; set; }
    public int Total { get; set; }
    public int Percentage => Total == 0 ? 0 : (int)Math.Round(Solved * 100d / Total);
}
