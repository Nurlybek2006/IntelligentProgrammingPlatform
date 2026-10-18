namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

// Сақталған шолудың тек көрсетуге арналған өрістерін біріктіреді.
public sealed record AcceptedCodeReviewViewModel(string Summary, string Readability, string Naming,
    string Efficiency, string Style, string TimeComplexity, string SpaceComplexity,
    string ComplexityExplanation, IReadOnlyList<string> Suggestions, DateTime CreatedAt);
