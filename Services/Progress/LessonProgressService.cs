using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Progress;

public sealed class LessonProgressService
{
    private readonly ApplicationDbContext _db;

    // Сабақты аяқтау тарихын сақтайтын контексті қабылдайды.
    public LessonProgressService(ApplicationDbContext db) => _db = db;

    // Жарияланған сабақты бір рет аяқтайды; қатар басылған батырма сыйақыны көбейтпейді.
    public async Task<bool> CompleteAsync(int lessonId, string userId, CancellationToken cancellationToken)
    {
        if (!await _db.Lessons.AsNoTracking().AnyAsync(lesson => lesson.Id == lessonId && lesson.IsPublished, cancellationToken)) return false;
        if (await _db.LessonProgresses.AnyAsync(item => item.UserId == userId && item.LessonId == lessonId, cancellationToken)) return true;
        var progress = new LessonProgress { LessonId = lessonId, UserId = userId };
        _db.LessonProgresses.Add(progress);
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsUniqueViolation(exception))
        {
            _db.Entry(progress).State = EntityState.Detached;
        }
        return true;
    }
}
