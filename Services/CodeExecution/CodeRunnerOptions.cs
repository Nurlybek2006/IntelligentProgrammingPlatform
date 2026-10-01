namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public static class CodeRunnerOptions
{
    public const string LanguageKey = "cpp";
    public const string Image = "gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15";
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
