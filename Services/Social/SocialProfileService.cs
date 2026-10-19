using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.ViewModels.Social;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Social;

public sealed class SocialProfileService
{
    private readonly ApplicationDbContext _db;
    private readonly ExperienceService _experience;
    private readonly FriendshipService _friendships;
    public const int PageSize = 20;

    // Ашық карточкаларға бар XP қызметін және жеке әлеуметтік күйді қосады.
    public SocialProfileService(ApplicationDbContext db, ExperienceService experience, FriendshipService friendships)
    { _db = db; _experience = experience; _friendships = friendships; }

    // Email, source, favorites, heatmap және хаттарсыз тек ашық профиль проекциясын қайтарады.
    public async Task<PublicProfileViewModel?> GetProfileAsync(string id, string? viewerId, CancellationToken cancellationToken)
    {
        var cards = await LoadCardsAsync(_db.Users.Where(user => user.Id == id), cancellationToken);
        var card = cards.SingleOrDefault();
        if (card == null) return null;
        var states = await _friendships.GetStatesAsync(viewerId, [card.Id], cancellationToken);
        return new(card, states[card.Id]);
    }

    // Достарды, сұрауларды, өз блоктарын немесе DisplayName іздеуін жиырмадан беттейді.
    public async Task<FriendsViewModel> GetFriendsAsync(string actorId, string? section, string? query, int page, CancellationToken cancellationToken)
    {
        section = new[] { "friends", "incoming", "outgoing", "search", "blocked" }.Contains(section) ? section! : "friends";
        query = (query ?? "").Trim();
        if (query.Length > 100) query = query[..100];
        page = Math.Clamp(page, 1, 100000);
        var users = _db.Users.AsNoTracking().Where(user => user.Id != actorId);
        if (section == "blocked") users = users.Where(user => _db.UserBlocks.Any(block => block.BlockerUserId == actorId && block.BlockedUserId == user.Id));
        else
        {
            users = users.Where(user => !_db.UserBlocks.Any(block =>
                (block.BlockerUserId == actorId && block.BlockedUserId == user.Id) || (block.BlockerUserId == user.Id && block.BlockedUserId == actorId)));
            if (section == "search")
                users = query.Length < 2 ? users.Where(_ => false)
                    : users.Where(user => EF.Functions.Collate(user.DisplayName, "Latin1_General_100_CI_AS").Contains(query));
            else
                users = users.Where(user => _db.Friendships.Any(friend =>
                    ((friend.UserAId == actorId && friend.UserBId == user.Id) || (friend.UserBId == actorId && friend.UserAId == user.Id))
                    && (section == "friends" ? friend.Status == FriendshipStatus.Accepted
                        : friend.Status == FriendshipStatus.Pending && (section == "incoming" ? friend.RequestedByUserId != actorId : friend.RequestedByUserId == actorId))));
        }
        var cards = await LoadCardsAsync(users.OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Skip((page - 1) * PageSize).Take(PageSize + 1), cancellationToken);
        var visible = cards.Take(PageSize).ToList();
        var states = await _friendships.GetStatesAsync(actorId, visible.Select(card => card.Id).ToArray(), cancellationToken);
        return new FriendsViewModel { Section = section, Query = query, Page = page, HasNextPage = cards.Count > PageSize,
            Users = visible.Select(card => new SocialUserViewModel(card, states[card.Id])).ToList() };
    }

    // Бір бет статистикасын топтап оқып, жеке Experience өрістерін ашық DTO-ға көшірмейді.
    private async Task<List<PublicUserCard>> LoadCardsAsync(IQueryable<ApplicationUser> users, CancellationToken cancellationToken)
    {
        var cards = await users.AsNoTracking().Select(user => new PublicUserCard
        {
            Id = user.Id, DisplayName = user.DisplayName, AvatarPath = user.AvatarPath, JoinedAt = user.CreatedAt,
            SolvedTasks = user.Leaderboard == null ? 0 : user.Leaderboard.SolvedTasks,
            Score = user.Leaderboard == null ? 0 : user.Leaderboard.Score,
            SuccessRate = user.Leaderboard == null || user.Leaderboard.TotalSubmissions == 0 ? 0
                : 100m * user.Leaderboard.SuccessfulSubmissions / user.Leaderboard.TotalSubmissions
        }).ToListAsync(cancellationToken);
        var experience = await _experience.GetForUsersAsync(cards.Select(card => card.Id).ToArray(), cancellationToken, includeFavorites: false);
        foreach (var card in cards) { card.Level = experience[card.Id].CurrentLevel; card.TotalXp = experience[card.Id].TotalXp; }
        return cards;
    }
}
