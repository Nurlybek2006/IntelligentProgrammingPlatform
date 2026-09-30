using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public sealed record TestRunResult(ExecutionStatus Status, string ActualOutput,
    string? ErrorMessage, int? ExitCode, int? ExecutionTimeMs, long? MemoryUsedKb = null);
