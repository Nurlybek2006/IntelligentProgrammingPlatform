namespace IntelligentProgrammingPlatform.Models;

public sealed class AcceptedCodeReview
{
    public long Id { get; set; }
    public long SubmissionId { get; set; }
    public string Model { get; set; } = "";
    public string Summary { get; set; } = "";
    public string ReadabilityFeedback { get; set; } = "";
    public string NamingFeedback { get; set; } = "";
    public string EfficiencyFeedback { get; set; } = "";
    public string StyleFeedback { get; set; } = "";
    public string TimeComplexity { get; set; } = "";
    public string SpaceComplexity { get; set; } = "";
    public string ComplexityExplanation { get; set; } = "";
    public string SuggestionsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public Submission Submission { get; set; } = null!;
}
