namespace IntelligentProgrammingPlatform.ViewModels.Leaderboard;

public class LeaderboardViewModel
{
    public int Page { get; set; }
    public bool HasNextPage { get; set; }
    public List<LeaderboardRowViewModel> Rows { get; set; } = new();
}

public class LeaderboardRowViewModel
{
    public int Rank { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int SolvedTasks { get; set; }
    public int SuccessfulSubmissions { get; set; }
    public int TotalSubmissions { get; set; }
    public int Score { get; set; }
    public bool IsCurrentUser { get; set; }
    public decimal SuccessRate => TotalSubmissions == 0 ? 0 : Math.Round(SuccessfulSubmissions * 100m / TotalSubmissions, 1);
}
