namespace IntelligentProgrammingPlatform.Models
{
    public class Leaderboard
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int SolvedTasks { get; set; }
        public int SuccessfulSubmissions { get; set; }
        public int TotalSubmissions { get; set; }
        public int Score { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User { get; set; } = null!;
    }
}
