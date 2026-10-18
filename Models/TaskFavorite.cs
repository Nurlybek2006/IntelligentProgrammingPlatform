namespace IntelligentProgrammingPlatform.Models;

public sealed class TaskFavorite
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public int ProgrammingTaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ApplicationUser User { get; set; } = null!;
    public ProgrammingTask ProgrammingTask { get; set; } = null!;
}
