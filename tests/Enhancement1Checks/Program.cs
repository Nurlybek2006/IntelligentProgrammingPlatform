using System.Text.Json;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using IntelligentProgrammingPlatform.Services.Security;
using Microsoft.Extensions.Logging.Abstractions;

await Checks.RunAsync();

static class Checks
{
    // Жіктеу басымдығын, nonce оқшаулығын және шектеулі нақты компиляцияларды тексереді.
    public static async Task RunAsync()
    {
        var command = new DockerCommandResult(137, "", "", false, false);
        Check(CompilationFailureClassifier.Classify(command, 137, true) == "Compilation memory limit exceeded.", "OOM + 137 is memory failure");
        Check(CompilationFailureClassifier.Classify(command with { TimedOut = true, OutputLimitExceeded = true }, 137, true)
            == "Compilation memory limit exceeded.", "Reliable OOM evidence has priority over other limits");
        Check(CompilationFailureClassifier.Classify(command, 137, false) == "Compilation was terminated before it completed.", "137 alone does not guess timeout or memory");
        Check(CompilationFailureClassifier.Classify(command, 124, false) == "Compilation time limit exceeded.", "GNU timeout is classified as time");
        Check(CompilationFailureClassifier.Classify(command with { TimedOut = true }, 137, false) == "Compilation time limit exceeded.", "Host watchdog is classified as time");
        Check(CompilationFailureClassifier.Classify(command with { OutputLimitExceeded = true }, 137, false) == "Compilation output limit exceeded.", "Output bound remains enforced");
        Check(CompilationFailureClassifier.Classify(command, 1, false) is null, "Ordinary syntax diagnostics remain available");

        var first = new ContentSecurityPolicy();
        var second = new ContentSecurityPolicy();
        Check(first.Nonce != second.Nonce && Convert.FromBase64String(first.Nonce).Length == 32, "Independent 256-bit request nonces");
        first.EnableMonacoStyles();
        Check(first.BuildHeader().Contains("style-src-attr 'unsafe-inline'") && second.BuildHeader().Contains("style-src-attr 'none'"), "Editor style allowance stays request scoped");
        Check(!first.BuildHeader().Contains("unsafe-eval") && first.BuildHeader().Contains("script-src-attr 'none'"), "No eval or event-handler allowance");

        var docker = new DockerCli();
        using var runner = new DockerCodeRunner(docker, NullLogger<DockerCodeRunner>.Instance);
        var image = await runner.GetTrustedImageAsync(CancellationToken.None);
        var workspace = new SubmissionWorkspace();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(workspace.Source, "main.cpp"), "int main() { syntax error }");
            var syntax = await runner.CompileAsync(workspace, image, CancellationToken.None);
            Check(!syntax.Succeeded && syntax.Output.Contains("error:") && !syntax.Output.Contains(workspace.Root)
                && !syntax.Output.Contains("/source/"), "Real syntax failure retains sanitized diagnostics");

            var source = "#define X0 0,\n" + string.Join("\n", Enumerable.Range(1, 23).Select(i => $"#define X{i} X{i - 1} X{i - 1}"))
                + "\nint values[] = { X23 };\nint main() {}\n";
            await File.WriteAllTextAsync(Path.Combine(workspace.Source, "main.cpp"), source);
            var timeout = await RestrictedCompileAsync(docker, workspace, image, 128, "0.01s");
            Check(!timeout.OomKilled && timeout.ExitCode == 124 && timeout.Message == "Compilation time limit exceeded.", "Real compiler timeout (test-only 10 ms deadline)");
            var oom = await RestrictedCompileAsync(docker, workspace, image, 32, "5s");
            Check(oom.OomKilled && oom.Message == "Compilation memory limit exceeded.", $"Real compiler OOM (test-only 32 MB cap, exit {oom.ExitCode})");
        }
        finally { workspace.Delete(); }
        Check(!Directory.Exists(workspace.Root), "Test workspace cleaned");
        Console.WriteLine("ENHANCEMENT 1 C# CHECKS PASSED");
    }

    // Тест компиляторын өндірістен де кіші лимиттермен іске қосып, нақты Docker күйін оқиды.
    private static async Task<(int ExitCode, bool OomKilled, string? Message)> RestrictedCompileAsync(
        DockerCli docker, SubmissionWorkspace workspace, string image, int memoryMb, string deadline)
    {
        var name = "ipp-enhancement1-" + Guid.NewGuid().ToString("N");
        var arguments = new[] {
            "create", "--name", name, "--label", "ipp.check=enhancement1", "--pull", "never",
            "--network", "none", "--cap-drop", "ALL", "--security-opt", "no-new-privileges",
            "--pids-limit", "64", "--memory", memoryMb + "m", "--memory-swap", memoryMb + "m",
            "--cpus", "1", "--read-only", "--user", "65534:65534", "--init", "--log-driver", "none",
            "--ulimit", "core=0:0", "--ulimit", "nofile=64:64", "--ulimit", "fsize=16777216:16777216",
            "--tmpfs", "/tmp:rw,noexec,nosuid,nodev,size=16m,mode=1777",
            "--mount", $"type=bind,source={workspace.Source},target=/source,readonly",
            "--workdir", "/tmp", "--entrypoint", "/usr/bin/timeout", image,
            "--signal=TERM", "--kill-after=0.1s", deadline, "/usr/local/bin/g++",
            "-std=c++20", "-fsyntax-only", "-fdiagnostics-color=never", "/source/main.cpp"
        };
        try
        {
            var created = await docker.ExecuteAsync(arguments, null, TimeSpan.FromSeconds(10), CancellationToken.None);
            Check(created.ExitCode == 0, "Restricted compiler fixture created");
            var command = await docker.ExecuteAsync(new[] { "start", "--attach", name }, null, TimeSpan.FromSeconds(10), CancellationToken.None);
            if (command.TimedOut || command.OutputLimitExceeded)
                await docker.ExecuteAsync(new[] { "kill", name }, null, TimeSpan.FromSeconds(5), CancellationToken.None);
            var inspected = await docker.ExecuteAsync(new[] { "inspect", "--format", "{{json .State}}", name }, null, TimeSpan.FromSeconds(5), CancellationToken.None);
            Check(inspected.ExitCode == 0, "Real compiler container state inspected");
            using var state = JsonDocument.Parse(inspected.Output);
            Check(!state.RootElement.GetProperty("Running").GetBoolean(), "Compiler fixture stopped");
            var exit = state.RootElement.GetProperty("ExitCode").GetInt32();
            var oom = state.RootElement.GetProperty("OOMKilled").GetBoolean();
            return (exit, oom, CompilationFailureClassifier.Classify(command, exit, oom));
        }
        finally
        {
            var removed = await docker.ExecuteAsync(new[] { "rm", "--force", name }, null, TimeSpan.FromSeconds(10), CancellationToken.None);
            Check(removed.ExitCode == 0, "Restricted compiler container cleaned");
        }
    }

    // Сәтсіз тексеруді тоқтатып, құпиясыз нәтижені шығарады.
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }
}
