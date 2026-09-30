namespace IntelligentProgrammingPlatform.Models
{
    public class Runtime
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LanguageKey { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public string? CompileCommand { get; set; }
        public string RunCommand { get; set; } = string.Empty;
        public string? DockerImage { get; set; }
        public bool IsEnabled { get; set; } = true;

        public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
