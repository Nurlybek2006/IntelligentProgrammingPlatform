using System.Globalization;
using System.Text;
using System.Text.Json;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace IntelligentProgrammingPlatform.Services.AI;

public sealed class AcceptedCodeReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly IAcceptedCodeReviewClient _client;
    private readonly AiRequestGate _gate;
    private readonly AiTutorOptions _options;
    private readonly ILogger<AcceptedCodeReviewService> _logger;
    public bool IsConfigured => _options.IsConfigured;

    // Меншік, AI лимиті, ортақ клиент және шолуды сақтау қызметтерін қабылдайды.
    public AcceptedCodeReviewService(ApplicationDbContext db, IAcceptedCodeReviewClient client, AiRequestGate gate,
        IOptions<AiTutorOptions> options, ILogger<AcceptedCodeReviewService> logger)
    {
        _db = db; _client = client; _gate = gate; _options = options.Value; _logger = logger;
    }

    // Иесінің Accepted кодын қолмен сұралғанда бір рет талдап, нәтижені қайта қолданады.
    public async Task<AcceptedCodeReviewStatus> AnalyzeAsync(long submissionId, string userId, CancellationToken cancellationToken)
    {
        var submission = await _db.Submissions.AsNoTracking().Where(item => item.Id == submissionId && item.UserId == userId)
            .Select(item => new { item.Status, item.FinishedAt }).SingleOrDefaultAsync(cancellationToken);
        if (submission == null) return AcceptedCodeReviewStatus.NotFound;
        if (submission.Status != SubmissionStatus.Accepted || submission.FinishedAt == null) return AcceptedCodeReviewStatus.NotAccepted;
        if (await _db.AcceptedCodeReviews.AnyAsync(item => item.SubmissionId == submissionId, cancellationToken)) return AcceptedCodeReviewStatus.Existing;
        if (!IsConfigured) return AcceptedCodeReviewStatus.NotConfigured;
        if (!_gate.TryEnter(submissionId, userId)) return AcceptedCodeReviewStatus.Busy;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 15, 60)));
            // SQL application lock бөлек сервер процестерінің де бір review-ді қатар сатып алуын тежейді.
            await using var transaction = await _db.Database.BeginTransactionAsync(deadline.Token);
            if (!await AcquireLockAsync(submissionId, deadline.Token)) return AcceptedCodeReviewStatus.Busy;
            if (await _db.AcceptedCodeReviews.AnyAsync(item => item.SubmissionId == submissionId, deadline.Token)) return AcceptedCodeReviewStatus.Existing;
            var input = await BuildInputAsync(submissionId, userId, deadline.Token);
            if (input == null) return AcceptedCodeReviewStatus.NotAccepted;
            _logger.LogInformation("Accepted review API request started for submission {SubmissionId}", submissionId);
            var response = await _client.ReviewAsync(input, deadline.Token);
            var content = AcceptedCodeReviewContent.Parse(response.Json);
            var review = new AcceptedCodeReview
            {
                SubmissionId = submissionId, Model = Clean(response.Model, 100), Summary = Clean(content.Summary, 600),
                ReadabilityFeedback = Clean(content.Readability, 800), NamingFeedback = Clean(content.Naming, 800),
                EfficiencyFeedback = Clean(content.Efficiency, 800), StyleFeedback = Clean(content.Style, 800),
                TimeComplexity = Clean(content.TimeComplexity, 120), SpaceComplexity = Clean(content.SpaceComplexity, 120),
                ComplexityExplanation = Clean(content.ComplexityExplanation, 1200),
                SuggestionsJson = JsonSerializer.Serialize(content.Suggestions.Select(item => Clean(item, 300))),
                CreatedAt = DateTime.UtcNow, InputTokens = response.InputTokens >= 0 ? response.InputTokens : null,
                OutputTokens = response.OutputTokens >= 0 ? response.OutputTokens : null
            };
            _db.AcceptedCodeReviews.Add(review);
            // Ақылы жауап келген соң клиент ажыратылса да сақтауға он секунд мүмкіндік береді.
            using var saveDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _db.SaveChangesAsync(saveDeadline.Token);
            await transaction.CommitAsync(saveDeadline.Token);
            return AcceptedCodeReviewStatus.Saved;
        }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsUniqueViolation(exception)) { return AcceptedCodeReviewStatus.Existing; }
        catch (Exception exception)
        {
            _logger.LogWarning("Accepted review unavailable for submission {SubmissionId} ({FailureType})", submissionId, exception.GetType().Name);
            return AcceptedCodeReviewStatus.Unavailable;
        }
        finally { _gate.Exit(submissionId); }
    }

    // Тек есеп мәтіні мен Accepted кодын алады; тест кестелерін мүлде оқымайды.
    public async Task<AcceptedCodeReviewInput?> BuildInputAsync(long submissionId, string userId, CancellationToken cancellationToken)
    {
        var item = await _db.Submissions.AsNoTracking()
            .Where(item => item.Id == submissionId && item.UserId == userId && item.Status == SubmissionStatus.Accepted && item.FinishedAt != null)
            .Select(item => new { item.ProgrammingTask.Title, item.ProgrammingTask.Description, item.ProgrammingTask.Difficulty,
                item.SourceCode, item.Runtime.LanguageKey }).SingleOrDefaultAsync(cancellationToken);
        if (item == null) return null;
        var language = RunnerLanguage.Find(item.LanguageKey) ?? throw new InvalidDataException("Unsupported language.");
        if (Encoding.UTF8.GetByteCount(item.SourceCode) > CodeRunnerOptions.MaxSourceBytes) throw new InvalidDataException("Source exceeds limit.");
        return new AcceptedCodeReviewInput(Clean(item.Title, 200), Clean(item.Description, 8000), item.Difficulty.ToString(),
            language.Name, Clean(item.SourceCode, CodeRunnerOptions.MaxSourceBytes))
        {
            ResponseLanguage = CultureInfo.CurrentUICulture.Name switch
            {
                "ru-RU" => AiResponseLanguage.Russian, "en-US" => AiResponseLanguage.English, _ => AiResponseLanguage.Kazakh
            }
        };
    }

    // Сақталған шолуды тек жіберілім иесіне проекциялап қайтарады.
    public async Task<AcceptedCodeReviewViewModel?> GetExistingAsync(long submissionId, string userId, CancellationToken cancellationToken)
    {
        var item = await _db.AcceptedCodeReviews.AsNoTracking().Where(item => item.SubmissionId == submissionId && item.Submission.UserId == userId)
            .Select(item => new { item.Summary, item.ReadabilityFeedback, item.NamingFeedback, item.EfficiencyFeedback, item.StyleFeedback,
                item.TimeComplexity, item.SpaceComplexity, item.ComplexityExplanation, item.SuggestionsJson, item.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (item == null) return null;
        string[] suggestions;
        try { suggestions = JsonSerializer.Deserialize<string[]>(item.SuggestionsJson)?.Where(value => !string.IsNullOrWhiteSpace(value)).Take(3).ToArray() ?? []; }
        catch (JsonException) { suggestions = []; }
        return new(item.Summary, item.ReadabilityFeedback, item.NamingFeedback, item.EfficiencyFeedback, item.StyleFeedback,
            item.TimeComplexity, item.SpaceComplexity, item.ComplexityExplanation, suggestions, item.CreatedAt);
    }

    // Транзакция біткенде босайтын атаулы SQL құлпын күтпей алуға әрекет етеді.
    private async Task<bool> AcquireLockAsync(long submissionId, CancellationToken cancellationToken)
    {
        await using var command = _db.Database.GetDbConnection().CreateCommand();
        command.Transaction = _db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=0; SELECT @result;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource"; parameter.Value = "ipp-accepted-review-" + submissionId.ToString(CultureInfo.InvariantCulture);
        command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) >= 0;
    }

    // Барлық AI өрістеріне ортақ құпия мен сервер жолын тазалау ережесін қолданады.
    private string Clean(string? value, int limit) => AiTextSanitizer.Clean(value, limit, _options.ApiKey);
}
