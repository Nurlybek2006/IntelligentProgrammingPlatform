namespace IntelligentProgrammingPlatform.ViewModels.Progress;

public sealed class SubmissionActivityViewModel
{
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public IReadOnlyList<ActivityDay> Days { get; init; } = Array.Empty<ActivityDay>();
    public long TotalSubmissions => Days.Sum(day => day.Submissions);
    public int LeadingDays => ((int)Start.DayOfWeek + 6) % 7;
}

// Бір UTC күнінің ресми әрекеттер санын және Accepted санын сақтайды.
public sealed record ActivityDay(DateTime Date, long Submissions, long Accepted)
{
    public int Intensity => Submissions switch { <= 0 => 0, 1 => 1, 2 => 2, <= 4 => 3, _ => 4 };
}
