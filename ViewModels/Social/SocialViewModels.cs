using System.ComponentModel.DataAnnotations;
using IntelligentProgrammingPlatform.Services.Social;
using IntelligentProgrammingPlatform.ViewModels.Account;

namespace IntelligentProgrammingPlatform.ViewModels.Social;

public sealed class PublicUserCard
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? AvatarPath { get; init; }
    public DateTime JoinedAt { get; init; }
    public long TotalXp { get; set; }
    public int Level { get; set; } = 1;
    public int SolvedTasks { get; init; }
    public int Score { get; init; }
    public decimal SuccessRate { get; init; }
    public UserAvatarViewModel Avatar => new() { DisplayName = DisplayName, AvatarPath = AvatarPath };
}

// Ашық профильге тек қауіпсіз карточка мен ағымдағы байланыс күйін береді.
public sealed record PublicProfileViewModel(PublicUserCard Profile, SocialRelationship Relationship);

// Бір қатысушыға қатысты рұқсат етілген әлеуметтік батырмаларды анықтайды.
public sealed record SocialActionsViewModel(string TargetUserId, SocialRelationship Relationship);

// Достар тізіміндегі карточканы сервер тексерген байланыс күйімен біріктіреді.
public sealed record SocialUserViewModel(PublicUserCard Profile, SocialRelationship Relationship);

public sealed class FriendsViewModel
{
    public string Section { get; init; } = "friends";
    public string Query { get; init; } = "";
    public int Page { get; init; } = 1;
    public bool HasNextPage { get; init; }
    public IReadOnlyList<SocialUserViewModel> Users { get; init; } = [];
}

// Навигацияға тек иесінің оқылмаған хаттары мен кіріс сұрауларының санын береді.
public sealed record SocialCounts(long UnreadMessages, long IncomingRequests);

public sealed class ConversationListItem
{
    public long Id { get; init; }
    public string OtherUserId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? AvatarPath { get; init; }
    public string Preview { get; init; } = "";
    public DateTime? LastMessageAt { get; init; }
    public long UnreadCount { get; init; }
    public UserAvatarViewModel Avatar => new() { DisplayName = DisplayName, AvatarPath = AvatarPath };
}

public sealed class ConversationListViewModel
{
    public int Page { get; init; } = 1;
    public bool HasNextPage { get; init; }
    public IReadOnlyList<ConversationListItem> Rows { get; init; } = [];
}

public sealed class DirectMessageViewModel
{
    public long Id { get; init; }
    public string Body { get; init; } = "";
    public string SenderName { get; init; } = "";
    public bool IsMine { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ConversationThreadViewModel
{
    public long Id { get; init; }
    public PublicUserCard OtherUser { get; init; } = new();
    public bool CanSend { get; set; }
    public bool HasOlder { get; set; }
    public bool IsOlderPage { get; init; }
    public IReadOnlyList<DirectMessageViewModel> Messages { get; set; } = [];
}

public sealed class MessagesViewModel
{
    public ConversationListViewModel Conversations { get; init; } = new();
    public ConversationThreadViewModel? Thread { get; init; }
    public Guid ClientMessageId { get; init; } = Guid.NewGuid();
}

public sealed class SendDirectMessageViewModel
{
    public long ConversationId { get; set; }
    public Guid ClientMessageId { get; set; }
    [Required(ErrorMessage = "Social_MessageRequired")]
    [StringLength(2000, ErrorMessage = "Social_MessageTooLong")]
    public string Body { get; set; } = "";
}
