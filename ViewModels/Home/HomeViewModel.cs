using IntelligentProgrammingPlatform.ViewModels.Submissions;
using IntelligentProgrammingPlatform.ViewModels.Progress;

namespace IntelligentProgrammingPlatform.ViewModels.Home;

public sealed class HomeViewModel
{
    public int SolvedTasks { get; set; }
    public int Score { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public decimal SuccessRate { get; set; }
    public ExperienceViewModel Experience { get; set; } = new();
    public PracticeNextViewModel? PracticeNext { get; set; }
    public List<SubmissionListItemViewModel> Recent { get; set; } = new();
}
