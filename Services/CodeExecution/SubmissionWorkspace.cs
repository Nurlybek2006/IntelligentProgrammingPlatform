using System.Text;

namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public sealed class SubmissionWorkspace
{
    public string Root { get; }
    public string Source { get; }
    public string Build { get; }

    // Бір жіберілімге арналған кездейсоқ, пайдаланушы деректерінен тәуелсіз бумаларды жасайды.
    public SubmissionWorkspace()
    {
        Root = Path.Combine(CodeRunnerOptions.TemporaryRoot, Guid.NewGuid().ToString("N"));
        Source = Path.Combine(Root, "source");
        Build = Path.Combine(Root, "build");
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Build);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            File.SetUnixFileMode(Source, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            File.SetUnixFileMode(Build, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        }
    }

    // Кодты shell-ге қоспай, ортақ UTF-8 файлға compiler оқитындай жазады.
    public async Task WriteSourceAsync(string sourceCode, CancellationToken cancellationToken)
    {
        var sourcePath = Path.Combine(Source, "main.cpp");
        await File.WriteAllTextAsync(sourcePath, sourceCode, new UTF8Encoding(false), cancellationToken);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(sourcePath, UnixFileMode.UserRead | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    // Тек осы жіберілімнің тексерілген уақытша бумасын жояды.
    public void Delete()
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(CodeRunnerOptions.TemporaryRoot), Path.GetFullPath(Root));
        if (!Guid.TryParseExact(relative, "N", out _))
            throw new InvalidOperationException("Invalid runner workspace cleanup path.");
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
