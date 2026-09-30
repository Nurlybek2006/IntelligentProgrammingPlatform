namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public sealed record AiFeedbackViewModel(string Summary, string Explanation, string ErrorCategory,
    IReadOnlyList<string> Hints, DateTime CreatedAt);
