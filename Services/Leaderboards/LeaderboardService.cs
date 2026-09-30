using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Leaderboard;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Leaderboards;

public sealed class LeaderboardService
{
    private readonly ApplicationDbContext _db;
    private readonly LeaderboardUpdateGate _gate;

    // Қайта есептеуге контекст пен ортақ жаңарту кезегін береді.
    public LeaderboardService(ApplicationDbContext db, LeaderboardUpdateGate gate)
    {
        _db = db;
        _gate = gate;
    }

    // Бір пайдаланушының материалданған нәтижесін толық тарихтан қайта есептейді.
    public Task RecalculateUserAsync(string userId, CancellationToken cancellationToken) =>
        RecalculateAsync(userId, onlyMissing: false, cancellationToken);

    // Барлық пайдаланушы кестесін тарихты өшірмей қайта құрады.
    public Task RebuildAsync(CancellationToken cancellationToken) =>
        RecalculateAsync(null, onlyMissing: false, cancellationToken);

    // Бұрынғы немесе жаңа пайдаланушыда жоқ summary жолдарын автоматты түрде толықтырады.
    public Task EnsureMissingAsync(CancellationToken cancellationToken) =>
        RecalculateAsync(null, onlyMissing: true, cancellationToken);

    // SQL агрегаттарынан unique solved ұпайларын және аяқталған әрекеттер санын сақтайды.
    private async Task RecalculateAsync(string? userId, bool onlyMissing, CancellationToken cancellationToken)
    {
        await _gate.EnterAsync(cancellationToken);
        try
        {
            var users = _db.Users.AsNoTracking().Where(user => userId == null || user.Id == userId);
            if (onlyMissing) users = users.Where(user => !_db.Leaderboards.Any(row => row.UserId == user.Id));
            var ids = await users.Select(user => user.Id).ToListAsync(cancellationToken);
            if (ids.Count == 0) return;
            var history = _db.Submissions.AsNoTracking().Where(item => ids.Contains(item.UserId));
            var counts = await history.Where(item => item.Status >= SubmissionStatus.Accepted
                && item.Status <= SubmissionStatus.InternalError).GroupBy(item => item.UserId)
                .Select(group => new
                {
                    UserId = group.Key, Total = group.Count(), Accepted = group.Count(item => item.Status == SubmissionStatus.Accepted)
                }).ToDictionaryAsync(item => item.UserId, cancellationToken);
            var scores = await history.Where(item => item.Status == SubmissionStatus.Accepted)
                .Select(item => new { item.UserId, item.ProgrammingTaskId, item.ProgrammingTask.Difficulty }).Distinct()
                .GroupBy(item => item.UserId).Select(group => new
                {
                    UserId = group.Key, Solved = group.Count(),
                    Score = group.Sum(item => item.Difficulty == Difficulty.Easy ? 100
                        : item.Difficulty == Difficulty.Medium ? 200 : item.Difficulty == Difficulty.Hard ? 300 : 0)
                }).ToDictionaryAsync(item => item.UserId, cancellationToken);
            var existing = await _db.Leaderboards.Where(row => ids.Contains(row.UserId))
                .ToDictionaryAsync(row => row.UserId, cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var id in ids)
            {
                if (!existing.TryGetValue(id, out var row))
                {
                    row = new Leaderboard { UserId = id };
                    _db.Leaderboards.Add(row);
                }
                row.SolvedTasks = scores.GetValueOrDefault(id)?.Solved ?? 0;
                row.Score = scores.GetValueOrDefault(id)?.Score ?? 0;
                row.TotalSubmissions = counts.GetValueOrDefault(id)?.Total ?? 0;
                row.SuccessfulSubmissions = counts.GetValueOrDefault(id)?.Accepted ?? 0;
                row.UpdatedAt = now;
            }
            await _db.SaveChangesAsync(cancellationToken);
        }
        finally { _gate.Exit(); }
    }

    // Рейтингті тұрақты ретпен беттеп, тек ашық профиль атауы мен агрегаттарды шығарады.
    public async Task<LeaderboardViewModel> GetPageAsync(string? currentUserId, int page, CancellationToken cancellationToken)
    {
        await EnsureMissingAsync(cancellationToken);
        page = Math.Clamp(page, 1, 100000);
        var rows = await _db.Leaderboards.AsNoTracking().OrderByDescending(row => row.Score)
            .ThenByDescending(row => row.SolvedTasks).ThenBy(row => row.UserId)
            .Skip((page - 1) * 50).Take(51).Select(row => new LeaderboardRowViewModel
            {
                DisplayName = row.User.DisplayName, SolvedTasks = row.SolvedTasks,
                SuccessfulSubmissions = row.SuccessfulSubmissions, TotalSubmissions = row.TotalSubmissions,
                Score = row.Score, IsCurrentUser = row.UserId == currentUserId
            }).ToListAsync(cancellationToken);
        for (var index = 0; index < Math.Min(rows.Count, 50); index++) rows[index].Rank = (page - 1) * 50 + index + 1;
        return new LeaderboardViewModel { Page = page, HasNextPage = rows.Count > 50, Rows = rows.Take(50).ToList() };
    }
}
