using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Social;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Social;

public sealed class SocialNotificationService
{
    private readonly ApplicationDbContext _db;

    // Жеке хабар санағыштарын есептейтін контекстті қабылдайды.
    public SocialNotificationService(ApplicationDbContext db) => _db = db;

    // Тек қатысушының кіріс оқылмаған хаттары мен Pending кіріс сұрауларын SQL-де санайды.
    public async Task<SocialCounts> GetCountsAsync(string userId, CancellationToken cancellationToken)
    {
        var unread = await _db.DirectMessages.AsNoTracking().LongCountAsync(message => message.ReadAt == null
            && message.SenderUserId != userId && (message.Conversation.UserAId == userId || message.Conversation.UserBId == userId), cancellationToken);
        var incoming = await _db.Friendships.AsNoTracking().LongCountAsync(friend => friend.Status == FriendshipStatus.Pending
            && friend.RequestedByUserId != userId && (friend.UserAId == userId || friend.UserBId == userId), cancellationToken);
        return new(unread, incoming);
    }
}
