using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public class SubmissionDetailsViewModel
{
    public long Id { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string TaskSlug { get; set; } = string.Empty;
    public string RuntimeName { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public bool? CompileSucceeded { get; set; }
    public string? CompilerOutput { get; set; }
    public int PassedTests { get; set; }
    public int TotalTests { get; set; }
    public int? ExecutionTimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public List<TestResultViewModel> Results { get; set; } = new();
    public AiFeedbackViewModel? AiFeedback { get; set; }
    public bool AiConfigured { get; set; }
}

public class TestResultViewModel
{
    public int Number { get; set; }
    public bool IsHidden { get; set; }
    public ExecutionStatus Status { get; set; }
    public int? ExecutionTimeMs { get; set; }
    public long? MemoryUsedKb { get; set; }
    public string? Input { get; set; }
    public string? ExpectedOutput { get; set; }
    public string? ActualOutput { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SubmissionListItemViewModel
{
    public long Id { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string RuntimeName { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public int PassedTests { get; set; }
    public int TotalTests { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionHistoryViewModel
{
    public List<SubmissionListItemViewModel> Items { get; set; } = new();
    public int Page { get; set; }
    public bool HasNextPage { get; set; }
}
