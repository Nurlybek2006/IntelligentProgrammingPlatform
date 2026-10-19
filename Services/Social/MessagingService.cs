using System.Data.Common;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.ViewModels.Social;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Social;

public sealed class MessagingService
{
    private readonly ApplicationDbContext _db;
    private readonly SocialPairLock _pairs;
    private readonly SocialNotifier _notifications;
    private readonly TimeProvider _clock;
    private readonly ILogger<MessagingService> _logger;
    public const int MaxMessageLength = 2000;
    public const int MessagePageSize = 50;

    // Хат сақтауына ортақ жұп құлпын, уақытты және commit-тен кейінгі хабарлауды береді.
    public MessagingService(ApplicationDbContext db, SocialPairLock pairs, SocialNotifier notifications,
        TimeProvider clock, ILogger<MessagingService> logger)
    { _db = db; _pairs = pairs; _notifications = notifications; _clock = clock; _logger = logger; }

    // Тек Accepted достарға жаңа диалог ашады; бұрынғы қатысушыларға сақталған диалогты қайтарады.
    public async Task<SocialResult> OpenAsync(string actorId, string? targetId, CancellationToken cancellationToken)
    {
        var pair = await _pairs.ResolveAsync(actorId, targetId, cancellationToken);
        if (pair == null) return new(SocialOutcome.NotAllowed);
        try
        {
            await using var transaction = await _pairs.BeginAsync(pair, cancellationToken);
            var existing = await _db.DirectConversations.AsNoTracking().Where(item => item.UserAId == pair.UserAId && item.UserBId == pair.UserBId)
                .Select(item => (long?)item.Id).SingleOrDefaultAsync(cancellationToken);
            if (existing != null) return new(SocialOutcome.Unchanged, existing);
            if (!await _pairs.CanMessageAsync(pair, cancellationToken)) return new(SocialOutcome.Unavailable);
            var conversation = new DirectConversation { UserAId = pair.UserAId, UserBId = pair.UserBId, CreatedAt = _clock.GetUtcNow().UtcDateTime };
            _db.DirectConversations.Add(conversation);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(SocialOutcome.Changed, conversation.Id);
        }
        catch (TimeoutException) { return new(SocialOutcome.Busy); }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        { _logger.LogWarning("Conversation creation failed ({FailureType})", exception.GetType().Name); return new(SocialOutcome.Failed); }
    }

    // Қатысушыны, қазіргі достық пен блокты тексеріп, қайталанған POST-ты бір хатқа біріктіреді.
    public async Task<SocialResult> SendAsync(string actorId, long conversationId, string? body, Guid clientMessageId, CancellationToken cancellationToken)
    {
        body = body?.Trim();
        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxMessageLength || clientMessageId == Guid.Empty) return new(SocialOutcome.InvalidMessage);
        var participant = await _db.DirectConversations.AsNoTracking().Where(item => item.Id == conversationId
            && (item.UserAId == actorId || item.UserBId == actorId))
            .Select(item => new { item.UserAId, item.UserBId }).SingleOrDefaultAsync(cancellationToken);
        if (participant == null) return new(SocialOutcome.NotFound);
        var pair = SocialPair.Create(participant.UserAId, participant.UserBId);
        long messageId;
        try
        {
            await using var transaction = await _pairs.BeginAsync(pair, cancellationToken);
            var conversation = await _db.DirectConversations.SingleOrDefaultAsync(item => item.Id == conversationId
                && (item.UserAId == actorId || item.UserBId == actorId), cancellationToken);
            if (conversation == null) return new(SocialOutcome.NotFound);
            if (!await _pairs.CanMessageAsync(pair, cancellationToken)) return new(SocialOutcome.Unavailable);
            var existing = await _db.DirectMessages.AsNoTracking().Where(item => item.SenderUserId == actorId && item.ClientMessageId == clientMessageId)
                .Select(item => new { item.Id, item.ConversationId, item.Body }).SingleOrDefaultAsync(cancellationToken);
            if (existing != null) return existing.ConversationId == conversationId && existing.Body == body
                ? new(SocialOutcome.Unchanged, existing.Id) : new(SocialOutcome.InvalidMessage);
            var now = _clock.GetUtcNow().UtcDateTime;
            var message = new DirectMessage { ConversationId = conversationId, SenderUserId = actorId, Body = body,
                ClientMessageId = clientMessageId, CreatedAt = now };
            _db.DirectMessages.Add(message);
            conversation.LastMessageAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            messageId = message.Id;
        }
        catch (TimeoutException) { return new(SocialOutcome.Busy); }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        { _logger.LogWarning("Direct message persistence failed ({FailureType})", exception.GetType().Name); return new(SocialOutcome.Failed); }
        await _notifications.NotifyAsync([pair.UserAId, pair.UserBId], "NewMessage", new { conversationId, messageId });
        return new(SocialOutcome.Changed, messageId);
    }

    // Қатысушы диалогтарының соңғы хабарын және unread санын шектелген SQL проекциясымен алады.
    public async Task<ConversationListViewModel> GetListAsync(string actorId, int page, CancellationToken cancellationToken)
    {
        page = Math.Clamp(page, 1, 100000);
        var rows = await _db.DirectConversations.AsNoTracking().Where(item => item.UserAId == actorId || item.UserBId == actorId)
            .OrderByDescending(item => item.LastMessageAt ?? item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * 20).Take(21).Select(item => new ConversationListItem
            {
                Id = item.Id, OtherUserId = item.UserAId == actorId ? item.UserBId : item.UserAId,
                DisplayName = item.UserAId == actorId ? item.UserB.DisplayName : item.UserA.DisplayName,
                AvatarPath = item.UserAId == actorId ? item.UserB.AvatarPath : item.UserA.AvatarPath,
                LastMessageAt = item.LastMessageAt,
                Preview = item.Messages.OrderByDescending(message => message.Id).Select(message => message.Body.Substring(0, 100)).FirstOrDefault() ?? "",
                UnreadCount = item.Messages.LongCount(message => message.ReadAt == null && message.SenderUserId != actorId)
            }).ToListAsync(cancellationToken);
        return new() { Page = page, HasNextPage = rows.Count > 20, Rows = rows.Take(20).ToList() };
    }

    // Авторизацияны SQL сүзгісінде қолданып, соңғы не одан бұрынғы елу хатты қайтарады.
    public async Task<ConversationThreadViewModel?> GetThreadAsync(string actorId, long conversationId, long? beforeId, CancellationToken cancellationToken)
    {
        var conversation = await _db.DirectConversations.AsNoTracking().Where(item => item.Id == conversationId
            && (item.UserAId == actorId || item.UserBId == actorId))
            .Select(item => new { item.UserAId, item.UserBId,
                OtherId = item.UserAId == actorId ? item.UserBId : item.UserAId,
                Name = item.UserAId == actorId ? item.UserB.DisplayName : item.UserA.DisplayName,
                Avatar = item.UserAId == actorId ? item.UserB.AvatarPath : item.UserA.AvatarPath }).SingleOrDefaultAsync(cancellationToken);
        if (conversation == null) return null;
        if (beforeId.HasValue && !await _db.DirectMessages.AsNoTracking().AnyAsync(item => item.Id == beforeId && item.ConversationId == conversationId, cancellationToken)) return null;
        var messages = await _db.DirectMessages.AsNoTracking().Where(item => item.ConversationId == conversationId
            && (beforeId == null || item.Id < beforeId))
            .OrderByDescending(item => item.Id).Take(MessagePageSize + 1).Select(item => new DirectMessageViewModel
            { Id = item.Id, Body = item.Body, SenderName = item.SenderUser.DisplayName, CreatedAt = item.CreatedAt, IsMine = item.SenderUserId == actorId })
            .ToListAsync(cancellationToken);
        return new ConversationThreadViewModel
        {
            Id = conversationId, OtherUser = new PublicUserCard { Id = conversation.OtherId, DisplayName = conversation.Name, AvatarPath = conversation.Avatar },
            IsOlderPage = beforeId.HasValue, HasOlder = messages.Count > MessagePageSize,
            Messages = messages.Take(MessagePageSize).Reverse().ToList(),
            CanSend = await _pairs.CanMessageAsync(SocialPair.Create(conversation.UserAId, conversation.UserBId), cancellationToken)
        };
    }

    // Тек алушының экранға дейін жеткен кіріс хаттарын POST сұрауымен оқылған деп белгілейді.
    public async Task<SocialResult> MarkReadAsync(string actorId, long conversationId, long throughId, CancellationToken cancellationToken)
    {
        if (!await _db.DirectConversations.AsNoTracking().AnyAsync(item => item.Id == conversationId
            && (item.UserAId == actorId || item.UserBId == actorId), cancellationToken)) return new(SocialOutcome.NotFound);
        if (throughId <= 0 || !await _db.DirectMessages.AsNoTracking().AnyAsync(item => item.Id == throughId && item.ConversationId == conversationId, cancellationToken))
            return new(SocialOutcome.NotFound);
        var changed = await _db.DirectMessages.Where(item => item.ConversationId == conversationId && item.Id <= throughId
            && item.SenderUserId != actorId && item.ReadAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.ReadAt, _clock.GetUtcNow().UtcDateTime), cancellationToken);
        if (changed > 0) await _notifications.NotifyAsync([actorId], "UnreadCountChanged", new { });
        return new(changed > 0 ? SocialOutcome.Changed : SocialOutcome.Unchanged);
    }
}
