namespace IntelligentProgrammingPlatform.Models;

public sealed class DirectMessage
{
    public long Id { get; set; }
    public long ConversationId { get; set; }
    public string SenderUserId { get; set; } = "";
    public Guid ClientMessageId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DirectConversation Conversation { get; set; } = null!;
    public ApplicationUser SenderUser { get; set; } = null!;
}
