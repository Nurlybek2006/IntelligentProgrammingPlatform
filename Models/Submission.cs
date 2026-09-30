using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.Models
{
    public class Submission
    {
        public long Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int ProgrammingTaskId { get; set; }
        public int RuntimeId { get; set; }
        public string SourceCode { get; set; } = string.Empty;
        public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public int? ExecutionTimeMs { get; set; }
        public int PassedTests { get; set; }
        public int TotalTests { get; set; }
        public bool? CompileSucceeded { get; set; }
        public string? CompilerOutput { get; set; }

        public ApplicationUser User { get; set; } = null!;
        public ProgrammingTask ProgrammingTask { get; set; } = null!;
        public Runtime Runtime { get; set; } = null!;
        public ICollection<ExecutionResult> ExecutionResults { get; set; } = new List<ExecutionResult>();
        public AiFeedback? AiFeedback { get; set; }
    }
}
