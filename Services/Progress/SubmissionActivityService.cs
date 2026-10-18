using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Progress;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Progress;

public sealed class SubmissionActivityService
{
    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    // Белсенділіктің күн шекарасын бір сенімді UTC уақыт көзімен байланыстырады.
    public SubmissionActivityService(ApplicationDbContext db, TimeProvider clock) { _db = db; _clock = clock; }

    // Соңғы 365 күнді SQL-де топтап, бос күндерді жадта толықтырады.
    public async Task<SubmissionActivityViewModel> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var end = _clock.GetUtcNow().UtcDateTime.Date;
        var start = end.AddDays(-364);
        var exclusiveEnd = end.AddDays(1);
        var rows = await _db.Submissions.AsNoTracking()
            .Where(item => item.UserId == userId && item.CreatedAt >= start && item.CreatedAt < exclusiveEnd)
            .GroupBy(item => item.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Count = group.LongCount(), Accepted = group.LongCount(item => item.Status == SubmissionStatus.Accepted) })
            .ToDictionaryAsync(item => item.Date, cancellationToken);
        return new SubmissionActivityViewModel
        {
            Start = start, End = end,
            Days = Enumerable.Range(0, 365).Select(offset =>
            {
                var date = start.AddDays(offset);
                var row = rows.GetValueOrDefault(date);
                return new ActivityDay(date, row?.Count ?? 0, row?.Accepted ?? 0);
            }).ToArray()
        };
    }
}
