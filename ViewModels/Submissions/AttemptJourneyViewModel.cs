using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public sealed class AttemptJourneyViewModel
{
    public string TaskTitle { get; set; } = string.Empty;
    public string TaskSlug { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public int TotalAttempts { get; set; }
    public int Page { get; set; } = 1;
    public int PageCount { get; set; } = 1;
    public bool IsCompact { get; set; }
    public List<JourneyAttemptViewModel> Attempts { get; set; } = new();
}

public sealed class JourneyAttemptViewModel
{
    public int Number { get; set; }
    public long SubmissionId { get; set; }
    public long? PreviousSubmissionId { get; set; }
    public string RuntimeName { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public int PassedTests { get; set; }
    public int TotalTests { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ExecutionTimeMs { get; set; }
    public bool HasAiFeedback { get; set; }
    public int AvailableHintCount { get; set; }
    public int RevealedHintCount { get; set; }
    public bool IsAccepted => Status == SubmissionStatus.Accepted;
}
