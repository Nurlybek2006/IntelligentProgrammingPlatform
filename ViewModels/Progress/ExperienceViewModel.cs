namespace IntelligentProgrammingPlatform.ViewModels.Progress;

public sealed class ExperienceViewModel
{
    public long TaskXp { get; init; }
    public long LessonXp { get; init; }
    public long TotalXp => TaskXp + LessonXp;
    public int CurrentLevel { get; init; } = 1;
    public long CurrentLevelXp { get; init; }
    public long XpNeededForNextLevel { get; init; } = 250;
    public decimal Percentage => 100m * CurrentLevelXp / XpNeededForNextLevel;
    public long CompletedLessons { get; init; }
    public long FavoritesCount { get; init; }
}
