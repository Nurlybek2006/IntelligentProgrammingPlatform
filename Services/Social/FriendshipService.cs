using System.Data.Common;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Social;

public sealed class FriendshipService
{
    private readonly ApplicationDbContext _db;
    private readonly SocialPairLock _pairs;
    private readonly SocialNotifier _notifications;
    private readonly TimeProvider _clock;
    private readonly ILogger<FriendshipService> _logger;

    // Әлеуметтік байланыстарға транзакция, сенімді уақыт және хабарлау қызметтерін береді.
    public FriendshipService(ApplicationDbContext db, SocialPairLock pairs, SocialNotifier notifications,
        TimeProvider clock, ILogger<FriendshipService> logger)
    { _db = db; _pairs = pairs; _notifications = notifications; _clock = clock; _logger = logger; }

    // Тізімдегі аккаунттардың өзара күйін екі сұраумен, блок бағытын бөтен адамға ашпай есептейді.
    public async Task<Dictionary<string, SocialRelationship>> GetStatesAsync(string? actorId, string[] targets, CancellationToken cancellationToken)
    {
        var states = targets.Distinct().ToDictionary(id => id, id => actorId == null ? SocialRelationship.Guest
            : actorId == id ? SocialRelationship.Self : SocialRelationship.None);
        if (actorId == null || targets.Length == 0) return states;
        var friendships = await _db.Friendships.AsNoTracking().Where(item =>
            (item.UserAId == actorId && targets.Contains(item.UserBId)) || (item.UserBId == actorId && targets.Contains(item.UserAId)))
            .Select(item => new { item.UserAId, item.UserBId, item.RequestedByUserId, item.Status }).ToListAsync(cancellationToken);
        foreach (var item in friendships)
            states[item.UserAId == actorId ? item.UserBId : item.UserAId] = item.Status == FriendshipStatus.Accepted
                ? SocialRelationship.Friends : item.RequestedByUserId == actorId ? SocialRelationship.Outgoing : SocialRelationship.Incoming;
        var blocks = await _db.UserBlocks.AsNoTracking().Where(item =>
            (item.BlockerUserId == actorId && targets.Contains(item.BlockedUserId)) || (item.BlockedUserId == actorId && targets.Contains(item.BlockerUserId)))
            .Select(item => new { item.BlockerUserId, item.BlockedUserId }).ToListAsync(cancellationToken);
        foreach (var item in blocks.Where(item => item.BlockedUserId == actorId)) states[item.BlockerUserId] = SocialRelationship.Unavailable;
        foreach (var item in blocks.Where(item => item.BlockerUserId == actorId)) states[item.BlockedUserId] = SocialRelationship.Blocked;
        return states;
    }

    // Достық пен блок әрекетін бір жұптың құлпы ішінде тек ағымдағы қатысушы атынан орындайды.
    public async Task<SocialResult> ChangeAsync(string actorId, string? targetId, FriendshipAction action, CancellationToken cancellationToken)
    {
        var pair = await _pairs.ResolveAsync(actorId, targetId, cancellationToken);
        if (pair == null) return new(SocialOutcome.NotAllowed);
        var otherId = pair.UserAId == actorId ? pair.UserBId : pair.UserAId;
        var eventName = "SocialStateChanged";
        try
        {
            await using var transaction = await _pairs.BeginAsync(pair, cancellationToken);
            var relationship = await _db.Friendships.SingleOrDefaultAsync(item => item.UserAId == pair.UserAId && item.UserBId == pair.UserBId, cancellationToken);
            var blocks = await _db.UserBlocks.Where(item => (item.BlockerUserId == actorId && item.BlockedUserId == otherId)
                || (item.BlockerUserId == otherId && item.BlockedUserId == actorId)).ToListAsync(cancellationToken);
            var ownBlock = blocks.SingleOrDefault(item => item.BlockerUserId == actorId);
            if (action == FriendshipAction.Block)
            {
                if (ownBlock != null && relationship == null) return new(SocialOutcome.Unchanged);
                if (ownBlock == null) _db.UserBlocks.Add(new UserBlock { BlockerUserId = actorId, BlockedUserId = otherId, CreatedAt = _clock.GetUtcNow().UtcDateTime });
                if (relationship != null) _db.Friendships.Remove(relationship);
            }
            else if (action == FriendshipAction.Unblock)
            {
                if (ownBlock == null) return new(SocialOutcome.Unchanged);
                _db.UserBlocks.Remove(ownBlock);
            }
            else
            {
                if (blocks.Count != 0) return new(SocialOutcome.Unavailable);
                switch (action)
                {
                    case FriendshipAction.Request:
                        if (relationship != null) return new(SocialOutcome.Unchanged);
                        _db.Friendships.Add(new Friendship { UserAId = pair.UserAId, UserBId = pair.UserBId,
                            RequestedByUserId = actorId, Status = FriendshipStatus.Pending, CreatedAt = _clock.GetUtcNow().UtcDateTime });
                        eventName = "FriendRequestReceived";
                        break;
                    case FriendshipAction.Accept:
                        if (relationship == null || relationship.RequestedByUserId == actorId) return new(SocialOutcome.NotAllowed);
                        if (relationship.Status == FriendshipStatus.Accepted) return new(SocialOutcome.Unchanged);
                        relationship.Status = FriendshipStatus.Accepted; relationship.AcceptedAt = _clock.GetUtcNow().UtcDateTime;
                        eventName = "FriendRequestAccepted";
                        break;
                    case FriendshipAction.Decline:
                    case FriendshipAction.Cancel:
                        if (relationship == null || relationship.Status != FriendshipStatus.Pending
                            || (action == FriendshipAction.Cancel) != (relationship.RequestedByUserId == actorId)) return new(SocialOutcome.NotAllowed);
                        _db.Friendships.Remove(relationship);
                        break;
                    case FriendshipAction.Remove:
                        if (relationship?.Status != FriendshipStatus.Accepted) return new(SocialOutcome.NotAllowed);
                        _db.Friendships.Remove(relationship);
                        break;
                    default: return new(SocialOutcome.NotAllowed);
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (TimeoutException) { return new(SocialOutcome.Busy); }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            _logger.LogWarning("Social relationship change failed ({FailureType})", exception.GetType().Name);
            return new(SocialOutcome.Failed);
        }
        await _notifications.NotifyAsync([actorId, otherId], eventName, new { });
        return new(SocialOutcome.Changed);
    }
}
