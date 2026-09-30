namespace IntelligentProgrammingPlatform.Models
{
    public class TestCase
    {
        public int Id { get; set; }
        public int ProgrammingTaskId { get; set; }
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public bool IsHidden { get; set; }
        public int Order { get; set; }

        public ProgrammingTask ProgrammingTask { get; set; } = null!;
        public ICollection<ExecutionResult> ExecutionResults { get; set; } = new List<ExecutionResult>();
    }
}
