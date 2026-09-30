namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public static class CodeRunnerOptions
{
    public const string LanguageKey = "cpp";
    public const string Image = "gcc:14.3.0-bookworm";
    public const int MaxSourceBytes = 64 * 1024;
    public const int MaxOutputBytes = 64 * 1024;
    public const int CompileTimeoutSeconds = 15;
    public const int CompileMemoryMb = 512;
    public const int MaxConcurrentSubmissions = 2;
    public const int MaxTests = 100;
    public const int MaxTestInputBytes = 1024 * 1024;
    public const int SubmissionTimeoutSeconds = 180;
    public const string UnavailableMessage = "Execution service is temporarily unavailable.";

    public static string TemporaryRoot => Path.Combine(Path.GetTempPath(), "IntelligentProgrammingPlatformRunner");
}
