using IntelligentProgrammingPlatform.ViewModels.Submissions;

namespace IntelligentProgrammingPlatform.ViewModels.Home;

public sealed class HomeViewModel
{
    public int SolvedTasks { get; set; }
    public int Score { get; set; }
    public List<SubmissionListItemViewModel> Recent { get; set; } = new();
}
