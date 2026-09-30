using Microsoft.AspNetCore.Identity;

namespace IntelligentProgrammingPlatform.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
        public Leaderboard? Leaderboard { get; set; }
    }
}
