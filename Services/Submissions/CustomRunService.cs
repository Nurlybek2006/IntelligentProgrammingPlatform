using System.Text;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.ViewModels.Submissions;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class CustomRunService
{
    private readonly DockerCodeRunner _runner;
    private readonly SubmissionExecutionGate _gate;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<CustomRunService> _logger;

    // Дерекқорға жазу қызметін қоспай, ортақ runner мен орындау кезегін қабылдайды.
    public CustomRunService(DockerCodeRunner runner, SubmissionExecutionGate gate,
        IHostApplicationLifetime lifetime, ILogger<CustomRunService> logger)
    {
        _runner = runner;
        _gate = gate;
        _lifetime = lifetime;
        _logger = logger;
    }

    // Уақытша кодты студент input-ымен орындап, ешбір submission немесе статистика сақтамайды.
    public async Task<CustomRunResult> RunAsync(string source, string input, int timeLimitMs,
        int memoryLimitMb, CancellationToken cancellationToken, RunnerLanguage? language = null)
    {
        SubmissionWorkspace? workspace = null;
        var entered = false;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(CodeRunnerOptions.CustomRunTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
            deadline.Token, _lifetime.ApplicationStopping);
        try
        {
            if (string.IsNullOrWhiteSpace(source) || Encoding.UTF8.GetByteCount(source) > CodeRunnerOptions.MaxSourceBytes
                || Encoding.UTF8.GetByteCount(input) > CodeRunnerOptions.MaxCustomInputBytes)
                throw new InvalidOperationException("Custom run input exceeds the validated limits.");
            entered = await _gate.EnterAsync(linked.Token);
            if (!entered) throw new InvalidOperationException("Runner capacity wait timed out.");
            var image = await _runner.GetTrustedImageAsync(linked.Token, language);
            workspace = new SubmissionWorkspace(language);
            await workspace.WriteSourceAsync(source, linked.Token);
            var compilation = await _runner.CompileAsync(workspace, image, linked.Token);
            if (!compilation.Succeeded)
                return new CustomRunResult { Status = CustomRunStatus.CompilationError, CompileSucceeded = false,
                    CompilerOutput = compilation.Output, Error = "Compilation failed." };
            var result = await _runner.RunCustomAsync(workspace, image, input, timeLimitMs, memoryLimitMb, linked.Token);
            return new CustomRunResult
            {
                Status = result.Status switch
                {
                    ExecutionStatus.Passed => CustomRunStatus.Success,
                    ExecutionStatus.RuntimeError => CustomRunStatus.RuntimeError,
                    ExecutionStatus.TimeLimitExceeded => CustomRunStatus.TimeLimitExceeded,
                    ExecutionStatus.MemoryLimitExceeded => CustomRunStatus.MemoryLimitExceeded,
                    _ => CustomRunStatus.InternalError
                },
                CompileSucceeded = true, CompilerOutput = compilation.Output, Output = result.ActualOutput,
                Error = result.ErrorMessage, ExitCode = result.ExitCode, ExecutionTimeMs = result.ExecutionTimeMs
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Custom run failed");
            return new CustomRunResult { Status = CustomRunStatus.InternalError, Error = CodeRunnerOptions.UnavailableMessage };
        }
        finally
        {
            if (workspace != null)
            {
                try { workspace.Delete(); }
                catch (Exception exception) { _logger.LogError(exception, "Custom run workspace cleanup failed"); }
            }
            if (entered) _gate.Exit();
        }
    }
}
