using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.Leaderboards;
using IntelligentProgrammingPlatform.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Profile;

public sealed class AdminUsersService
{
    private readonly ApplicationDbContext _db;
    private readonly LeaderboardService _leaderboard;

    // Әкімші тізіміне дерекқор мен бар рейтинг қызметін береді.
    public AdminUsersService(ApplicationDbContext db, LeaderboardService leaderboard)
    {
        _db = db; _leaderboard = leaderboard;
    }

    // Бір бетке тек қауіпсіз бағандарды және рөлдерді топтап оқиды.
    public async Task<UsersViewModel> GetAsync(int page, CancellationToken cancellationToken)
    {
        await _leaderboard.EnsureMissingAsync(cancellationToken);
        page = Math.Clamp(page, 1, 100000);
        var rows = await _db.Users.AsNoTracking().OrderByDescending(user => user.CreatedAt).ThenBy(user => user.Id)
            .Skip((page - 1) * 20).Take(21).Select(user => new UserListItemViewModel
            {
                Id = user.Id, DisplayName = user.DisplayName, Email = user.Email ?? "", AvatarPath = user.AvatarPath,
                CreatedAt = user.CreatedAt, SolvedTasks = user.Leaderboard == null ? 0 : user.Leaderboard.SolvedTasks,
                Score = user.Leaderboard == null ? 0 : user.Leaderboard.Score
            }).ToListAsync(cancellationToken);
        var model = new UsersViewModel { Page = page, HasNextPage = rows.Count > 20, Users = rows.Take(20).ToList() };
        var ids = model.Users.Select(user => user.Id).ToArray();
        var roles = await (from membership in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on membership.RoleId equals role.Id
            where ids.Contains(membership.UserId) orderby role.Name
            select new { membership.UserId, Name = role.Name ?? "" }).ToListAsync(cancellationToken);
        var byUser = roles.ToLookup(role => role.UserId, role => role.Name);
        foreach (var user in model.Users) user.Roles = byUser[user.Id].ToList();
        return model;
    }
}

