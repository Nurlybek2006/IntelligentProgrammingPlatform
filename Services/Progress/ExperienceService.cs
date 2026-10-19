using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Progress;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Progress;

public sealed class ExperienceService
{
    private readonly ApplicationDbContext _db;
    public const int LessonReward = 20;

    // XP есептеуіне тек оқу тарихын беретін контексті қабылдайды.
    public ExperienceService(ApplicationDbContext db) => _db = db;

    // Бір аккаунттың XP көрсеткішін өзгермейтін тарихтан есептейді.
    public async Task<ExperienceViewModel> GetAsync(string userId, CancellationToken cancellationToken) =>
        (await GetForUsersAsync([userId], cancellationToken))[userId];

    // Әкімші бетінің аккаунттарын үш SQL агрегатымен, код мәтінінсіз есептейді.
    public async Task<Dictionary<string, ExperienceViewModel>> GetForUsersAsync(string[] userIds, CancellationToken cancellationToken, bool includeFavorites = true)
    {
        if (userIds.Length == 0) return new();
        var tasks = await _db.Submissions.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId) && item.Status == SubmissionStatus.Accepted)
            .Select(item => new { item.UserId, item.ProgrammingTaskId, item.ProgrammingTask.Difficulty }).Distinct()
            .GroupBy(item => item.UserId).Select(group => new
            {
                UserId = group.Key,
                Xp = group.Sum(item => item.Difficulty == Difficulty.Easy ? 50L
                    : item.Difficulty == Difficulty.Medium ? 100L : item.Difficulty == Difficulty.Hard ? 200L : 0L)
            }).ToDictionaryAsync(item => item.UserId, item => item.Xp, cancellationToken);
        var lessons = await _db.LessonProgresses.AsNoTracking().Where(item => userIds.Contains(item.UserId))
            .GroupBy(item => item.UserId).Select(group => new { UserId = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);
        var favorites = includeFavorites ? await _db.TaskFavorites.AsNoTracking().Where(item => userIds.Contains(item.UserId))
            .GroupBy(item => item.UserId).Select(group => new { UserId = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken) : new Dictionary<string, long>();
        return userIds.Distinct().ToDictionary(id => id,
            id => Calculate(tasks.GetValueOrDefault(id), lessons.GetValueOrDefault(id), favorites.GetValueOrDefault(id)));
    }

    // Қиындықтың жеке оқу сыйақысын рейтинг формуласынан бөлек анықтайды.
    public static int TaskReward(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 50, Difficulty.Medium => 100, Difficulty.Hard => 200, _ => 0
    };

    // Деңгей шектерін екілік іздеумен, бөлшек дөңгелектеусіз анықтайды.
    public static ExperienceViewModel Calculate(long taskXp, long completedLessons = 0, long favoritesCount = 0)
    {
        if (taskXp < 0 || completedLessons < 0 || favoritesCount < 0) throw new ArgumentOutOfRangeException(nameof(taskXp));
        var lessonXp = checked(completedLessons * LessonReward);
        var total = checked(taskXp + lessonXp);
        var low = 1;
        var high = int.MaxValue;
        while (low < high)
        {
            var middle = low + (int)(((long)high - low + 1) / 2);
            if (250m * (middle - 1) * middle / 2 <= total) low = middle;
            else high = middle - 1;
        }
        var threshold = (long)(250m * (low - 1) * low / 2);
        return new ExperienceViewModel
        {
            TaskXp = taskXp, LessonXp = lessonXp, CompletedLessons = completedLessons, FavoritesCount = favoritesCount,
            CurrentLevel = low, CurrentLevelXp = total - threshold, XpNeededForNextLevel = 250L * low
        };
    }
}
