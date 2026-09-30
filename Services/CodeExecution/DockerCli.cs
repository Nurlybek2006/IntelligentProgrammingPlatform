using System.Diagnostics;
using System.Text;

namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public sealed class DockerCli
{
    private readonly string _executable;

    // Docker-дің белгілі орнату жолын таңдайды; браузерден executable қабылдамайды.
    public DockerCli()
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Docker", "Docker", "resources", "bin", "docker.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "DockerDesktop", "resources", "bin", "docker.exe")
            }
            : new[] { "/usr/bin/docker", "/usr/local/bin/docker" };
        _executable = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    // Тек Docker CLI-ды жеке аргументтермен іске қосып, уақыт пен шығыс көлемін шектейді.
    public async Task<DockerCommandResult> ExecuteAsync(IEnumerable<string> arguments, string? input,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(timeout);
        using var overflow = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token, overflow.Token);
        var start = new ProcessStartInfo
        {
            FileName = _executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (!process.Start())
            throw new InvalidOperationException("Docker CLI could not start.");

        var stdout = ReadBoundedAsync(process.StandardOutput.BaseStream, overflow, linked.Token);
        var stderr = ReadBoundedAsync(process.StandardError.BaseStream, overflow, linked.Token);
        var stdin = WriteInputAsync(process, input, linked.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            // Бұл процесс тек Docker клиенті; контейнерді runner finally бөлімінде бөлек жояды.
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }

        await stdin;
        var output = await stdout;
        var error = await stderr;
        cancellationToken.ThrowIfCancellationRequested();
        return new DockerCommandResult(process.ExitCode, output, error,
            deadline.IsCancellationRequested, overflow.IsCancellationRequested);
    }

    // STDOUT немесе STDERR ағынын шектен асырмай оқып, артық шығыста процесті тоқтатады.
    private static async Task<string> ReadBoundedAsync(Stream stream, CancellationTokenSource overflow,
        CancellationToken cancellationToken)
    {
        using var captured = new MemoryStream();
        var buffer = new byte[4096];
        try
        {
            while (true)
            {
                var count = await stream.ReadAsync(buffer, cancellationToken);
                if (count == 0) break;
                var remaining = CodeRunnerOptions.MaxOutputBytes - (int)captured.Length;
                captured.Write(buffer, 0, Math.Min(remaining, count));
                if (count > remaining)
                {
                    overflow.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        return Encoding.UTF8.GetString(captured.ToArray());
    }

    // Тест кірісін командаға қоспай, процестің STDIN ағынына жазады.
    private static async Task WriteInputAsync(Process process, string? input, CancellationToken cancellationToken)
    {
        try
        {
            if (input != null)
                await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
        }
        catch (IOException) { } // Бағдарлама кірісті толық оқымай аяқталуы мүмкін.
        catch (OperationCanceledException) { }
        finally
        {
            try { process.StandardInput.Close(); }
            catch (IOException) { }
        }
    }
}

public sealed record DockerCommandResult(int ExitCode, string Output, string Error,
    bool TimedOut, bool OutputLimitExceeded);
