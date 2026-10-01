using System.Text.Json.Serialization;

namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

[JsonConverter(typeof(JsonStringEnumConverter<CustomRunStatus>))]
public enum CustomRunStatus
{
    Success, CompilationError, RuntimeError, TimeLimitExceeded, MemoryLimitExceeded, InternalError
}

public sealed class CustomRunResult
{
    public CustomRunStatus Status { get; init; }
    public string Output { get; init; } = string.Empty;
    public string? Error { get; init; }
    public string? CompilerOutput { get; init; }
    public int? ExitCode { get; init; }
    public int? ExecutionTimeMs { get; init; }
    public bool? CompileSucceeded { get; init; }
}
