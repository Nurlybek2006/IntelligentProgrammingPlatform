using IntelligentProgrammingPlatform.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.AI;

public sealed class HintRevealService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HintRevealService> _logger;

    // Тек SQL күйін жаңартатын қызметке контекст пен қауіпсіз журнал береді.
    public HintRevealService(ApplicationDbContext db, ILogger<HintRevealService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Иесінің келесі кеңесін шартты SQL жаңартуымен ашады; OpenAI шақырмайды.
    public async Task<bool> RevealNextAsync(long submissionId, string userId, CancellationToken cancellationToken)
    {
        var owned = _db.AiFeedbacks.Where(item => item.SubmissionId == submissionId
            && item.UserId == userId && item.Submission.UserId == userId);
        var feedback = await owned.AsNoTracking()
            .Select(item => new { item.Id, item.HintsJson, item.RevealedHintCount }).SingleOrDefaultAsync(cancellationToken);
        if (feedback == null) return false;
        var available = StoredHintReader.Read(feedback.HintsJson, _logger, feedback.Id).Length;
        var next = Math.Min(Math.Clamp(feedback.RevealedHintCount, 0, available) + 1, available);
        if (next == feedback.RevealedHintCount) return true;
        // Бір бастапқы күйді оқыған қос сұрау бір деңгейді ғана ашады; басқа өрістер өзгермейді.
        await owned.Where(item => item.Id == feedback.Id && item.RevealedHintCount == feedback.RevealedHintCount
                && item.HintsJson == feedback.HintsJson)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RevealedHintCount, next), cancellationToken);
        return true;
    }
}
