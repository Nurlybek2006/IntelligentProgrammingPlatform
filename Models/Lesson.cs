namespace IntelligentProgrammingPlatform.Models;

public class Lesson
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? CodeExample { get; set; }
    public string? CodeLanguage { get; set; }
    public int TopicId { get; set; }
    public int Order { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Topic Topic { get; set; } = null!;
}
