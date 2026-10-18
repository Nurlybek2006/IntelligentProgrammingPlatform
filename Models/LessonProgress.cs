namespace IntelligentProgrammingPlatform.Models;

public sealed class LessonProgress
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public int LessonId { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser User { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
}
