using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public sealed class SubmissionComparisonViewModel
{
    public ComparedAttemptViewModel Previous { get; set; } = new();
    public ComparedAttemptViewModel Current { get; set; } = new();
}

public sealed class ComparedAttemptViewModel
{
    public long Id { get; set; }
    public int ProgrammingTaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string TaskSlug { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ExecutionTimeMs { get; set; }
}
