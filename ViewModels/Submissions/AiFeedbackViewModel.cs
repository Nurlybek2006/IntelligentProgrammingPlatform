namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public sealed record AiFeedbackViewModel(string Summary, string Explanation, string ErrorCategory,
    IReadOnlyList<string> Hints, DateTime CreatedAt, int AvailableHintCount)
{
    public int RevealedHintCount => Hints.Count;
    public bool CanRevealNext => RevealedHintCount < AvailableHintCount;
}
