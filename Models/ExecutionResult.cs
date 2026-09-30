using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.Models
{
    public class ExecutionResult
    {
        public long Id { get; set; }
        public long SubmissionId { get; set; }
        public int TestCaseId { get; set; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;
        public string? ActualOutput { get; set; }
        public string? ErrorMessage { get; set; }
        public int? ExitCode { get; set; }
        public int? ExecutionTimeMs { get; set; }
        public long? MemoryUsedKb { get; set; }

        public Submission Submission { get; set; } = null!;
        public TestCase TestCase { get; set; } = null!;
    }
}
