namespace IntelligentProgrammingPlatform.ViewModels.Account;

public class ProfileViewModel
{
    public IntelligentProgrammingPlatform.ViewModels.Progress.ExperienceViewModel Experience { get; set; } = new();
    public IntelligentProgrammingPlatform.ViewModels.Progress.SubmissionActivityViewModel Activity { get; set; } = new();
    public string? AvatarPath { get; set; }
    public IntelligentProgrammingPlatform.ViewModels.Progress.ProgressViewModel Progress { get; set; } = new();
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}
