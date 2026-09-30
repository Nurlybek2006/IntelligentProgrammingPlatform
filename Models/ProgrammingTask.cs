using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.Models
{
    public class ProgrammingTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Difficulty Difficulty { get; set; } = Difficulty.Easy;
        public int TopicId { get; set; }
        public int TimeLimitMs { get; set; } = 2000;
        public int MemoryLimitMb { get; set; } = 256;
        public bool IsPublished { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Topic Topic { get; set; } = null!;
        public ICollection<TestCase> TestCases { get; set; } = new List<TestCase>();
        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
