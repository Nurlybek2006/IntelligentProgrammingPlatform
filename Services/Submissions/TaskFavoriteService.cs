using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Tasks;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class TaskFavoriteService
{
    private readonly ApplicationDbContext _db;

    // Жеке таңдаулыларды сақтауға дерекқор контекстін қабылдайды.
    public TaskFavoriteService(ApplicationDbContext db) => _db = db;

    // Иесінің жарияланған таңдаулы есептерін қауіпсіз метадеректермен қайтарады.
    public Task<List<TaskListItemViewModel>> GetAsync(string userId, CancellationToken cancellationToken) =>
        _db.TaskFavorites.AsNoTracking().Where(item => item.UserId == userId && item.ProgrammingTask.IsPublished)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new TaskListItemViewModel
            {
                Id = item.ProgrammingTaskId, Title = item.ProgrammingTask.Title, Slug = item.ProgrammingTask.Slug,
                TopicName = item.ProgrammingTask.Topic.Name, Difficulty = item.ProgrammingTask.Difficulty, IsFavorite = true,
                IsSolved = item.ProgrammingTask.Submissions.Any(submission => submission.UserId == userId && submission.Status == SubmissionStatus.Accepted)
            }).ToListAsync(cancellationToken);

    // Қайталанатын қосу мен өшіру сұрауларын тек берілген иеге қолданады.
    public async Task<bool> SetAsync(int taskId, string userId, bool favorite, CancellationToken cancellationToken)
    {
        var query = _db.TaskFavorites.Where(item => item.UserId == userId && item.ProgrammingTaskId == taskId);
        if (!favorite) { await query.ExecuteDeleteAsync(cancellationToken); return true; }
        if (!await _db.ProgrammingTasks.AsNoTracking().AnyAsync(task => task.Id == taskId && task.IsPublished, cancellationToken)) return false;
        if (await query.AnyAsync(cancellationToken)) return true;
        var item = new TaskFavorite { UserId = userId, ProgrammingTaskId = taskId };
        _db.TaskFavorites.Add(item);
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsUniqueViolation(exception))
        {
            _db.Entry(item).State = EntityState.Detached;
        }
        return true;
    }
}
