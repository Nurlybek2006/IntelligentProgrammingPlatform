using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.Models;

public sealed class Friendship
{
    public long Id { get; set; }
    public string UserAId { get; set; } = "";
    public string UserBId { get; set; } = "";
    public string RequestedByUserId { get; set; } = "";
    public FriendshipStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public ApplicationUser UserA { get; set; } = null!;
    public ApplicationUser UserB { get; set; } = null!;
    public ApplicationUser RequestedByUser { get; set; } = null!;
}
