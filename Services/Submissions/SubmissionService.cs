using System.Text;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.Services.Leaderboards;
using IntelligentProgrammingPlatform.ViewModels.Submissions;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class SubmissionService
{
    private readonly ApplicationDbContext _db;
    private readonly DockerCodeRunner _runner;
    private readonly SubmissionExecutionGate _gate;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SubmissionService> _logger;
    private readonly LeaderboardService _leaderboard;

    // Сақтау, Docker орындау және параллельдік шектеу қызметтерін біріктіреді.
    public SubmissionService(ApplicationDbContext db, DockerCodeRunner runner, SubmissionExecutionGate gate,
        IHostApplicationLifetime lifetime, ILogger<SubmissionService> logger, LeaderboardService leaderboard)
    {
        _db = db;
        _runner = runner;
        _gate = gate;
        _lifetime = lifetime;
        _logger = logger;
        _leaderboard = leaderboard;
    }

    // Рұқсат етілген жіберілімді алдымен сақтап, Docker аяқталған соң нәтижесін жаңартады.
    public async Task<SubmissionOutcome> SubmitAsync(string userId, SubmitViewModel model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.SourceCode)
            || Encoding.UTF8.GetByteCount(model.SourceCode) > CodeRunnerOptions.MaxSourceBytes)
            return new SubmissionOutcome(null, "Enter source code of at most 64 KB (UTF-8).");

        var task = await _db.ProgrammingTasks.AsNoTracking()
            .SingleOrDefaultAsync(task => task.Id == model.ProgrammingTaskId && task.IsPublished, cancellationToken);
        var runtime = await _db.Runtimes.AsNoTracking()
            .SingleOrDefaultAsync(runtime => runtime.Id == model.RuntimeId && runtime.IsEnabled, cancellationToken);
        if (task == null)
            return new SubmissionOutcome(null, "Select a published programming task.");
        if (runtime == null || runtime.LanguageKey != CodeRunnerOptions.LanguageKey)
            return new SubmissionOutcome(null, "Select an enabled C++ runtime.");

        var testQuery = _db.TestCases.AsNoTracking().Where(test => test.ProgrammingTaskId == task.Id);
        var count = await testQuery.CountAsync(cancellationToken);
        if (count == 0 || count > CodeRunnerOptions.MaxTests
            || await testQuery.AnyAsync(test => test.Input.Length > CodeRunnerOptions.MaxTestInputBytes
                || test.ExpectedOutput.Length > CodeRunnerOptions.MaxTestInputBytes, cancellationToken))
            return new SubmissionOutcome(null, "This task is not configured for execution. Please contact an administrator.");
        var tests = await testQuery.OrderBy(test => test.Order).ToListAsync(cancellationToken);
        if (tests.Any(test => Encoding.UTF8.GetByteCount(test.Input) > CodeRunnerOptions.MaxTestInputBytes
            || Encoding.UTF8.GetByteCount(test.ExpectedOutput) > CodeRunnerOptions.MaxTestInputBytes))
            return new SubmissionOutcome(null, "This task's test data exceeds the supported size.");

        var submission = new Submission
        {
            UserId = userId, ProgrammingTaskId = task.Id, RuntimeId = runtime.Id,
            SourceCode = model.SourceCode, Status = SubmissionStatus.Pending,
            CreatedAt = DateTime.UtcNow, TotalTests = tests.Count,
            ExecutionResults = tests.Select(test => new ExecutionResult
            {
                TestCaseId = test.Id, Status = ExecutionStatus.Pending
            }).ToList()
        };
        _db.Submissions.Add(submission);
        // Бір қысқа SaveChanges операциясы тесттерді тарихтан кездейсоқ өшіруден де қорғайды.
        await _db.SaveChangesAsync(cancellationToken);
        await ExecuteAsync(submission, tests, task.TimeLimitMs, task.MemoryLimitMb, cancellationToken);
        return new SubmissionOutcome(submission.Id, null);
    }

    // SQL транзакциясын ашық ұстамай, компиляция мен тесттерді шектеулі орындау орны арқылы жүргізеді.
    private async Task ExecuteAsync(Submission submission, List<TestCase> tests, int timeLimitMs,
        int memoryLimitMb, CancellationToken requestCancellation)
    {
        SubmissionWorkspace? workspace = null;
        var entered = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(CodeRunnerOptions.SubmissionTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            requestCancellation, _lifetime.ApplicationStopping, deadline.Token);
        try
        {
            entered = await _gate.EnterAsync(linked.Token);
            if (!entered) throw new InvalidOperationException("Runner capacity wait timed out.");
            submission.StartedAt = DateTime.UtcNow;
            submission.Status = SubmissionStatus.Compiling;
            await _db.SaveChangesAsync(linked.Token);

            var imageId = await _runner.GetTrustedImageAsync(linked.Token);
            workspace = new SubmissionWorkspace();
            var sourcePath = Path.Combine(workspace.Source, "main.cpp");
            await File.WriteAllTextAsync(sourcePath, submission.SourceCode, new UTF8Encoding(false), linked.Token);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(sourcePath, UnixFileMode.UserRead | UnixFileMode.UserWrite
                    | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            var compilation = await _runner.CompileAsync(workspace, imageId, linked.Token);
            submission.CompileSucceeded = compilation.Succeeded;
            submission.CompilerOutput = compilation.Output;
            if (!compilation.Succeeded)
            {
                submission.Status = SubmissionStatus.CompilationError;
                MarkRemainingSkipped(submission);
                return;
            }

            submission.Status = SubmissionStatus.Running;
            await _db.SaveChangesAsync(linked.Token);
            foreach (var test in tests)
            {
                var result = await _runner.RunTestAsync(workspace, imageId, test.Input, test.ExpectedOutput,
                    timeLimitMs, memoryLimitMb, linked.Token);
                var stored = submission.ExecutionResults.Single(item => item.TestCaseId == test.Id);
                stored.Status = result.Status;
                stored.ActualOutput = result.ActualOutput;
                stored.ErrorMessage = result.ErrorMessage;
                stored.ExitCode = result.ExitCode;
                stored.ExecutionTimeMs = result.ExecutionTimeMs;
                stored.MemoryUsedKb = result.MemoryUsedKb;
                submission.PassedTests = submission.ExecutionResults.Count(item => item.Status == ExecutionStatus.Passed);
                await _db.SaveChangesAsync(linked.Token);
            }
            submission.Status = DetermineStatus(submission.ExecutionResults);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Submission {SubmissionId} execution failed", submission.Id);
            submission.Status = SubmissionStatus.InternalError;
            var pending = submission.ExecutionResults.FirstOrDefault(result => result.Status == ExecutionStatus.Pending);
            if (pending != null)
            {
                pending.Status = ExecutionStatus.InternalError;
                pending.ErrorMessage = CodeRunnerOptions.UnavailableMessage;
            }
            MarkRemainingSkipped(submission);
        }
        finally
        {
            if (workspace != null)
            {
                try { workspace.Delete(); }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Workspace cleanup failed for submission {SubmissionId}", submission.Id);
                }
            }
            if (entered) _gate.Exit();
            submission.FinishedAt = DateTime.UtcNow;
            var measured = submission.ExecutionResults.Where(result => result.ExecutionTimeMs.HasValue).ToList();
            submission.ExecutionTimeMs = measured.Count == 0 ? null : measured.Sum(result => result.ExecutionTimeMs!.Value);
            // Сұрау жабылса да, соңғы күйді қысқа жеке операциямен сақтауға тырысамыз.
            using var saveDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var saved = false;
            try
            {
                await _db.SaveChangesAsync(saveDeadline.Token);
                saved = true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not persist final state for submission {SubmissionId}", submission.Id);
            }
            if (saved)
            {
                using var summaryDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await _leaderboard.RecalculateUserAsync(submission.UserId, summaryDeadline.Token); }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Leaderboard update failed after submission {SubmissionId}; an admin can rebuild it", submission.Id);
                }
            }
        }
    }

    // Компиляция не инфрақұрылым қатесінен кейін орындалмаған тесттерді Skipped деп белгілейді.
    private static void MarkRemainingSkipped(Submission submission)
    {
        foreach (var result in submission.ExecutionResults.Where(result => result.Status == ExecutionStatus.Pending))
            result.Status = ExecutionStatus.Skipped;
    }

    // Барлық тест өтсе Accepted, әйтпесе реті бойынша алғашқы қате түрін қайтарады.
    private static SubmissionStatus DetermineStatus(IEnumerable<ExecutionResult> results)
    {
        var failure = results.FirstOrDefault(result => result.Status != ExecutionStatus.Passed);
        return failure?.Status switch
        {
            null => SubmissionStatus.Accepted,
            ExecutionStatus.WrongAnswer => SubmissionStatus.WrongAnswer,
            ExecutionStatus.RuntimeError => SubmissionStatus.RuntimeError,
            ExecutionStatus.TimeLimitExceeded => SubmissionStatus.TimeLimitExceeded,
            ExecutionStatus.MemoryLimitExceeded => SubmissionStatus.MemoryLimitExceeded,
            _ => SubmissionStatus.InternalError
        };
    }
}

public sealed record SubmissionOutcome(long? Id, string? Error);
