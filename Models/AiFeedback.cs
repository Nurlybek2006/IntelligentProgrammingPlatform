namespace IntelligentProgrammingPlatform.Models;

public class AiFeedback
{
    public long Id { get; set; }
    public long SubmissionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string HintsJson { get; set; } = "[]";
    public int RevealedHintCount { get; set; } = 1;
    public string ErrorCategory { get; set; } = "Unknown";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public Submission Submission { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
