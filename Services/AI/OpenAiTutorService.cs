using System.ClientModel;
using System.Text;
using System.Text.Json;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IntelligentProgrammingPlatform.Services.AI;

public sealed class OpenAiTutorService
{
    private readonly ApplicationDbContext _db;
    private readonly IAiFeedbackClient _client;
    private readonly AiRequestGate _gate;
    private readonly AiTutorOptions _options;
    private readonly ILogger<OpenAiTutorService> _logger;
    public bool IsConfigured => _options.IsConfigured;

    // Меншік, шектеу, OpenAI клиенті және feedback сақтауға қажет қызметтерді алады.
    public OpenAiTutorService(ApplicationDbContext db, IAiFeedbackClient client, AiRequestGate gate,
        IOptions<AiTutorOptions> options, ILogger<OpenAiTutorService> logger)
    {
        _db = db;
        _client = client;
        _gate = gate;
        _options = options.Value;
        _logger = logger;
    }

    // Иесінің аяқталған жіберілімін бір рет талдап, тек жарамды кеңесті базаға сақтайды.
    public async Task<AiAnalysisResult> AnalyzeAsync(long submissionId, string userId, CancellationToken cancellationToken)
    {
        var submission = await _db.Submissions.AsNoTracking().Where(item => item.Id == submissionId && item.UserId == userId)
            .Select(item => new { item.Status, item.FinishedAt }).SingleOrDefaultAsync(cancellationToken);
        if (submission == null) return new(AiAnalysisStatus.NotFound);
        if (await _db.AiFeedbacks.AnyAsync(item => item.SubmissionId == submissionId, cancellationToken))
            return new(AiAnalysisStatus.Existing);
        if (submission.FinishedAt == null || submission.Status < SubmissionStatus.Accepted || submission.Status > SubmissionStatus.InternalError)
            return new(AiAnalysisStatus.NotFinished);
        if (!IsConfigured) return new(AiAnalysisStatus.NotConfigured);
        if (!_gate.TryEnter(submissionId, userId)) return new(AiAnalysisStatus.Busy);
        try
        {
            // Семафордан кейін қайта тексеру қатар келген сұраудағы ақылы қайталауды тоқтатады.
            if (await _db.AiFeedbacks.AnyAsync(item => item.SubmissionId == submissionId, cancellationToken))
                return new(AiAnalysisStatus.Existing);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 15, 60)));
            var input = await BuildInputAsync(submissionId, userId, deadline.Token);
            if (input == null) return new(AiAnalysisStatus.NotFound);
            var response = await _client.GenerateAsync(input, deadline.Token);
            var content = AiFeedbackContent.Parse(response.Json);
            var feedback = new AiFeedback
            {
                SubmissionId = submissionId, UserId = userId,
                Model = response.Model.Length <= 100 ? response.Model : _options.Model,
                Summary = Clean(content.Summary, 600), Explanation = Clean(content.Explanation, 3000),
                ErrorCategory = content.ErrorCategory,
                HintsJson = JsonSerializer.Serialize(content.Hints.Select(hint => Clean(hint, 300))),
                RevealedHintCount = 1,
                CreatedAt = DateTime.UtcNow,
                InputTokens = response.InputTokens >= 0 ? response.InputTokens : null,
                OutputTokens = response.OutputTokens >= 0 ? response.OutputTokens : null
            };
            _db.AiFeedbacks.Add(feedback);
            // API жауабы келген соң request үзілсе де ақылы нәтижені сақтауға қысқа мүмкіндік береді.
            using var saveDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _db.SaveChangesAsync(saveDeadline.Token);
            return new(AiAnalysisStatus.Saved);
        }
        catch (DbUpdateException exception) when (DbUpdateErrors.IsUniqueViolation(exception))
        {
            return new(AiAnalysisStatus.Existing);
        }
        catch (ClientResultException exception)
        {
            // API exception мәтінінде құпия болуы мүмкін: тек HTTP status журналға жазылады.
            _logger.LogWarning("AI request for submission {SubmissionId} failed with HTTP {Status}", submissionId, exception.Status);
            return new(AiAnalysisStatus.Unavailable);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("AI request for submission {SubmissionId} failed ({FailureType})", submissionId, exception.GetType().Name);
            return new(AiAnalysisStatus.Unavailable);
        }
        finally { _gate.Exit(submissionId); }
    }

    // Тек рұқсат етілген өрістерден DTO жасайды; hidden тесттің мәтіні SQL-дан да алынбайды.
    public async Task<AiTutorInput?> BuildInputAsync(long submissionId, string userId, CancellationToken cancellationToken)
    {
        var item = await _db.Submissions.AsNoTracking().Where(item => item.Id == submissionId && item.UserId == userId)
            .Select(item => new
            {
                item.ProgrammingTask.Title, item.ProgrammingTask.Description, item.ProgrammingTask.Difficulty,
                item.SourceCode, item.Status, item.CompilerOutput, item.PassedTests, item.TotalTests
            }).SingleOrDefaultAsync(cancellationToken);
        if (item == null) return null;
        if (Encoding.UTF8.GetByteCount(item.SourceCode) > CodeRunnerOptions.MaxSourceBytes)
            throw new InvalidDataException("Stored source exceeds the supported size.");
        var results = _db.ExecutionResults.AsNoTracking().Where(result => result.SubmissionId == submissionId);
        var visible = await results.Where(result => !result.TestCase.IsHidden && result.Status != ExecutionStatus.Passed
                && result.Status != ExecutionStatus.Skipped && result.Status != ExecutionStatus.Pending)
            .OrderBy(result => result.TestCase.Order).Take(3).Select(result => new
            {
                result.TestCase.Order, result.Status, result.TestCase.Input, result.TestCase.ExpectedOutput,
                result.ActualOutput, result.ErrorMessage
            }).ToListAsync(cancellationToken);
        var hidden = await results.Where(result => result.TestCase.IsHidden).OrderBy(result => result.TestCase.Order)
            .Take(100).Select(result => new { result.TestCase.Order, result.Status, result.ExecutionTimeMs })
            .ToListAsync(cancellationToken);
        return new AiTutorInput(Clean(item.Title, 200), Clean(item.Description, 8000), item.Difficulty.ToString(), "C++ 20",
            Clean(item.SourceCode, CodeRunnerOptions.MaxSourceBytes), item.Status.ToString(),
            item.Status == SubmissionStatus.CompilationError ? Clean(item.CompilerOutput, 6000) : null,
            item.PassedTests, item.TotalTests,
            visible.Select(result => new AiVisibleTest(result.Order, result.Status.ToString(), Clean(result.Input, 1000),
                Clean(result.ExpectedOutput, 1000), Clean(result.ActualOutput, 1000), Clean(result.ErrorMessage, 1000))).ToList(),
            hidden.Select(result => new AiHiddenTest(result.Order, result.Status.ToString(), result.ExecutionTimeMs)).ToList());
    }

    // Бұрын сақталған кеңесті тек оның иесіне арналған encoded ViewModel-ге түрлендіреді.
    public async Task<AiFeedbackViewModel?> GetExistingAsync(long submissionId, string userId, CancellationToken cancellationToken)
    {
        var feedback = await _db.AiFeedbacks.AsNoTracking().Where(item => item.SubmissionId == submissionId
                && item.Submission.UserId == userId && item.UserId == userId)
            .Select(item => new { item.Id, item.Summary, item.Explanation, item.ErrorCategory,
                item.HintsJson, item.RevealedHintCount, item.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        if (feedback == null) return null;
        var hints = StoredHintReader.Read(feedback.HintsJson, _logger, feedback.Id);
        var revealed = Math.Clamp(feedback.RevealedHintCount, 0, hints.Length);
        return new AiFeedbackViewModel(feedback.Summary, feedback.Explanation, feedback.ErrorCategory,
            hints.Take(revealed).ToArray(), feedback.CreatedAt, hints.Length);
    }

    // Бірдей redaction мен көлем шегін барлық AI мәтініне қолданады.
    private string Clean(string? value, int limit) => AiTextSanitizer.Clean(value, limit, _options.ApiKey);
}
