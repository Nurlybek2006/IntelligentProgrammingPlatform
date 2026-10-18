using System.Security.Claims;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Services.Progress;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class LessonCompletionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly LessonProgressService _progress;

    // Сабақтың жариялануы мен аяқталу тарихын тексеретін қызметтерді қабылдайды.
    public LessonCompletionsController(ApplicationDbContext db, LessonProgressService progress) { _db = db; _progress = progress; }

    [HttpPost]
    // Сабақты бір рет аяқтап, XP-ді келесі оқуда тарихтан қайта есептеуге қалдырады.
    public async Task<IActionResult> Complete(int lessonId, CancellationToken cancellationToken)
    {
        var slug = await _db.Lessons.AsNoTracking().Where(item => item.Id == lessonId && item.IsPublished)
            .Select(item => item.Slug).SingleOrDefaultAsync(cancellationToken);
        if (slug == null || !await _progress.CompleteAsync(lessonId, User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken)) return NotFound();
        return RedirectToAction("Details", "Lessons", new { slug });
    }
}
