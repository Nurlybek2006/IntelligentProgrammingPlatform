namespace IntelligentProgrammingPlatform.Models
{
    public class Topic
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public ICollection<ProgrammingTask> ProgrammingTasks { get; set; } = new List<ProgrammingTask>();
        public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    }
}
