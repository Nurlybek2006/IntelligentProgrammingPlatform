namespace IntelligentProgrammingPlatform.Models;

public sealed class UserBlock
{
    public long Id { get; set; }
    public string BlockerUserId { get; set; } = "";
    public string BlockedUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public ApplicationUser BlockerUser { get; set; } = null!;
    public ApplicationUser BlockedUser { get; set; } = null!;
}
