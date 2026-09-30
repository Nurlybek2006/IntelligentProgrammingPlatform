using System.Globalization;
using System.Text;
using System.Text.Json;
using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public sealed class DockerCodeRunner : IDisposable
{
    private readonly DockerCli _docker;
    private readonly ILogger<DockerCodeRunner> _logger;
    private readonly SemaphoreSlim _healthLock = new(1);
    private DateTime _healthExpiresAt;
    private string? _imageId;

    // Docker клиентін және қауіпсіз серверлік журналды runner-ге береді.
    public DockerCodeRunner(DockerCli docker, ILogger<DockerCodeRunner> logger)
    {
        _docker = docker;
        _logger = logger;
    }

    // Linux Docker мен бекітілген GCC image қолжетімділігін қысқа уақытқа кэштейді.
    public async Task<string> GetTrustedImageAsync(CancellationToken cancellationToken)
    {
        await _healthLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTime.UtcNow < _healthExpiresAt)
                return _imageId ?? throw new InvalidOperationException("Docker is unavailable (cached).");

            _imageId = null;
            _healthExpiresAt = DateTime.UtcNow.AddSeconds(15);
            var info = await _docker.ExecuteAsync(new[] { "info", "--format", "{{.OSType}}" }, null,
                TimeSpan.FromSeconds(5), cancellationToken);
            RequireSuccess(info, "Docker health check");
            if (info.Output.Trim() != "linux")
                throw new InvalidOperationException("The runner requires Linux containers.");

            var image = await _docker.ExecuteAsync(
                new[] { "image", "inspect", CodeRunnerOptions.Image, "--format", "{{.Id}}" },
                null, TimeSpan.FromSeconds(5), cancellationToken);
            RequireSuccess(image, "Trusted image lookup");
            var imageId = image.Output.Trim();
            if (!imageId.StartsWith("sha256:", StringComparison.Ordinal) || imageId.Length != 71
                || !imageId[7..].All(Uri.IsHexDigit))
                throw new InvalidOperationException("The trusted image ID is invalid.");
            _imageId = imageId;
            return imageId;
        }
        finally { _healthLock.Release(); }
    }

    // C++ бастапқы файлын шектеулі контейнерде бекітілген GCC аргументтерімен компиляциялайды.
    public async Task<CompileResult> CompileAsync(SubmissionWorkspace workspace, string imageId,
        CancellationToken cancellationToken)
    {
        var name = "ipp-compile-" + Guid.NewGuid().ToString("N");
        var arguments = CreateContainerArguments(name, CodeRunnerOptions.CompileMemoryMb, "1.0", 128);
        AddMount(arguments, workspace.Source, "/source", readOnly: true);
        AddMount(arguments, workspace.Build, "/build", readOnly: false);
        arguments.AddRange(new[]
        {
            "--workdir", "/build", "--entrypoint", "/usr/bin/timeout", imageId,
            "--signal=TERM", "--kill-after=0.1s", CodeRunnerOptions.CompileTimeoutSeconds + "s",
            "/usr/local/bin/g++", "-std=c++20", "-O2", "-pipe", "-fdiagnostics-color=never",
            "/source/main.cpp", "-o", "/build/program"
        });
        var result = await RunContainerAsync(name, arguments, null,
            TimeSpan.FromSeconds(CodeRunnerOptions.CompileTimeoutSeconds + 5), cancellationToken);

        if (result.Command.OutputLimitExceeded)
            return new CompileResult(false, "Compilation output limit exceeded.");
        if (result.Command.TimedOut || result.ExitCode is 124 or 137)
            return new CompileResult(false, "Compilation time or memory limit exceeded.");
        if (result.OomKilled)
            return new CompileResult(false, "Compilation memory limit exceeded.");
        if (result.ExitCode is 125 or 126 or 127)
            throw new InvalidOperationException("The compiler container could not start its trusted command.");

        var output = SanitizeDiagnostics(result.Command.Output + result.Command.Error, workspace);
        if (result.ExitCode != 0)
            return new CompileResult(false, string.IsNullOrWhiteSpace(output) ? "Compilation failed." : output);

        var executable = Path.Combine(workspace.Build, "program");
        if (!File.Exists(executable) || File.GetAttributes(executable).HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("The compiler did not produce a regular executable.");
        return new CompileResult(true, output);
    }

    // Бір тесті жаңа оқшауланған контейнерде орындап, нақты нәтижені бағалайды.
    public async Task<TestRunResult> RunTestAsync(SubmissionWorkspace workspace, string imageId,
        string input, string expectedOutput, int timeLimitMs, int memoryLimitMb, CancellationToken cancellationToken)
    {
        timeLimitMs = Math.Clamp(timeLimitMs, 100, 30000);
        memoryLimitMb = Math.Clamp(memoryLimitMb, 16, 1024);
        var name = "ipp-run-" + Guid.NewGuid().ToString("N");
        var arguments = CreateContainerArguments(name, memoryLimitMb, "0.5", 16);
        AddMount(arguments, workspace.Build, "/app", readOnly: true);
        arguments.AddRange(new[]
        {
            "--interactive", "--workdir", "/tmp", "--entrypoint", "/usr/bin/timeout", imageId,
            "--signal=TERM", "--kill-after=0.1s",
            (timeLimitMs / 1000d).ToString("F3", CultureInfo.InvariantCulture) + "s", "/app/program"
        });
        var result = await RunContainerAsync(name, arguments, input,
            TimeSpan.FromMilliseconds(timeLimitMs + 5000), cancellationToken);
        var output = result.Command.Output;
        if (result.Command.OutputLimitExceeded)
            return new TestRunResult(ExecutionStatus.RuntimeError, output, "Output limit exceeded.",
                result.ExitCode, result.ElapsedMs);
        if (result.OomKilled)
            return new TestRunResult(ExecutionStatus.MemoryLimitExceeded, output, "Memory limit exceeded.",
                result.ExitCode, result.ElapsedMs);
        if (result.Command.TimedOut || (result.ExitCode is 124 or 137 && result.ElapsedMs >= timeLimitMs))
            return new TestRunResult(ExecutionStatus.TimeLimitExceeded, output, "Time limit exceeded.",
                result.ExitCode, result.ElapsedMs);
        if (result.ExitCode != 0)
            return new TestRunResult(ExecutionStatus.RuntimeError, output, "Runtime error.",
                result.ExitCode, result.ElapsedMs);

        var status = NormalizeOutput(output) == NormalizeOutput(expectedOutput)
            ? ExecutionStatus.Passed : ExecutionStatus.WrongAnswer;
        // STDERR сақталуы мүмкін, бірақ hidden тест үшін контроллер оны ешқашан қайтармайды.
        var error = string.IsNullOrEmpty(result.Command.Error) ? null
            : SanitizeDiagnostics(result.Command.Error, workspace);
        return new TestRunResult(status, output, error, result.ExitCode, result.ElapsedMs);
    }

    // Жол аяқталуын теңестіріп, тек шығыстың соңындағы бос орындарды елемейді.
    public static string NormalizeOutput(string output) =>
        output.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();

    // Барлық контейнерге желі, құқық, процесс, жад, CPU және файл шектерін орнатады.
    private static List<string> CreateContainerArguments(string name, int memoryMb,
        string cpus, int temporaryMb) =>
        new()
        {
            "create", "--name", name, "--label", "ipp.runner=phase3",
            "--pull", "never", "--network", "none", "--cap-drop", "ALL",
            "--security-opt", "no-new-privileges", "--pids-limit", "64",
            "--memory", memoryMb + "m", "--memory-swap", memoryMb + "m", "--cpus", cpus,
            "--read-only", "--user", "65534:65534", "--init", "--log-driver", "none",
            "--ulimit", "core=0:0", "--ulimit", "nofile=64:64",
            "--ulimit", "fsize=16777216:16777216",
            "--tmpfs", "/tmp:rw,noexec,nosuid,nodev,size=" + temporaryMb + "m,mode=1777"
        };

    // Тек жіберілімнің тексерілген уақытша ішкі бумасын контейнерге жалғайды.
    private static void AddMount(List<string> arguments, string source, string target, bool readOnly)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(CodeRunnerOptions.TemporaryRoot), Path.GetFullPath(source));
        var parts = relative.Split(Path.DirectorySeparatorChar);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out _)
            || parts[1] is not ("source" or "build") || source.Contains(','))
            throw new InvalidOperationException("Invalid runner mount path.");
        arguments.Add("--mount");
        arguments.Add("type=bind,source=" + source + ",target=" + target + (readOnly ? ",readonly" : ""));
    }

    // Контейнерді жасап, ағындарын оқиды және кез келген аяқталуда міндетті түрде тазалайды.
    private async Task<ContainerResult> RunContainerAsync(string name, List<string> arguments,
        string? input, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _docker.ExecuteAsync(arguments, null, TimeSpan.FromSeconds(10), cancellationToken);
            RequireSuccess(created, "Container creation");
            var command = await _docker.ExecuteAsync(new[] { "start", "--attach", "--interactive", name },
                input, timeout, cancellationToken);

            // Host watchdog немесе output шегі іске қосылса, контейнерді inspect алдында тоқтатамыз.
            if (command.TimedOut || command.OutputLimitExceeded)
            {
                var killed = await _docker.ExecuteAsync(new[] { "kill", name }, null,
                    TimeSpan.FromSeconds(5), CancellationToken.None);
                if (killed.ExitCode != 0)
                    _logger.LogDebug("Container kill response for {Container}: {Error}", name, killed.Error);
            }

            var inspected = await _docker.ExecuteAsync(
                new[] { "inspect", "--type", "container", "--format", "{{json .State}}", name },
                null, TimeSpan.FromSeconds(5), CancellationToken.None);
            RequireSuccess(inspected, "Container result inspection");
            using var state = JsonDocument.Parse(inspected.Output);
            var root = state.RootElement;
            if (root.GetProperty("Running").GetBoolean() || root.GetProperty("StartedAt").GetString()!.StartsWith("0001"))
                throw new InvalidOperationException("The container did not finish its command.");
            var started = root.GetProperty("StartedAt").GetDateTimeOffset();
            var finished = root.GetProperty("FinishedAt").GetDateTimeOffset();
            var elapsed = (int)Math.Clamp((finished - started).TotalMilliseconds, 0, int.MaxValue);
            return new ContainerResult(command, root.GetProperty("ExitCode").GetInt32(),
                root.GetProperty("OOMKilled").GetBoolean(), elapsed);
        }
        finally { await RemoveContainerAsync(name); }
    }

    // Тоқтатылған не белсенді контейнерді сұрау cancellation-ынан тәуелсіз жоюға тырысады.
    private async Task RemoveContainerAsync(string name)
    {
        try
        {
            var removed = await _docker.ExecuteAsync(new[] { "rm", "--force", name }, null,
                TimeSpan.FromSeconds(10), CancellationToken.None);
            if (removed.ExitCode != 0 && !removed.Error.Contains("No such container", StringComparison.OrdinalIgnoreCase))
                _logger.LogError("Container cleanup failed for {Container}: {Error}", name, removed.Error);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Container cleanup failed for {Container}", name);
        }
    }

    // Docker инфрақұрылым қатесін тек сервер жағындағы exception ретінде көтереді.
    private static void RequireSuccess(DockerCommandResult result, string operation)
    {
        if (result.ExitCode != 0 || result.TimedOut || result.OutputLimitExceeded)
            throw new InvalidOperationException(operation + " failed: " + result.Error);
    }

    // Компилятор хабарынан host жолдарын алып, сақталатын диагностика көлемін шектейді.
    private static string SanitizeDiagnostics(string output, SubmissionWorkspace workspace)
    {
        output = output.Replace(workspace.Root, "[workspace]", StringComparison.OrdinalIgnoreCase)
            .Replace(CodeRunnerOptions.TemporaryRoot, "[workspace]", StringComparison.OrdinalIgnoreCase)
            .Replace("/source/main.cpp", "main.cpp", StringComparison.Ordinal)
            .Replace("/build/", "", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(output);
        return bytes.Length <= CodeRunnerOptions.MaxOutputBytes ? output
            : Encoding.UTF8.GetString(bytes, 0, CodeRunnerOptions.MaxOutputBytes) + "\n[Output truncated]";
    }

    // Қолданба тоқтағанда health-check семафорын босатады.
    public void Dispose() => _healthLock.Dispose();

    private sealed record ContainerResult(DockerCommandResult Command, int ExitCode, bool OomKilled, int ElapsedMs);
}
