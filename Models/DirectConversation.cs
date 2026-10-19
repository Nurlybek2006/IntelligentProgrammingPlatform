namespace IntelligentProgrammingPlatform.Models;

public sealed class DirectConversation
{
    public long Id { get; set; }
    public string UserAId { get; set; } = "";
    public string UserBId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public ApplicationUser UserA { get; set; } = null!;
    public ApplicationUser UserB { get; set; } = null!;
    public ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
}
