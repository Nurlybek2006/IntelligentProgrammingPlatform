namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public static class CompilationFailureClassifier
{
    // Docker OOM дерегіне басымдық беріп, дәлелсіз 137 кодын timeout деп болжамайды.
    public static string? Classify(DockerCommandResult command, int exitCode, bool oomKilled)
    {
        if (oomKilled) return "Compilation memory limit exceeded.";
        if (command.OutputLimitExceeded) return "Compilation output limit exceeded.";
        if (command.TimedOut || exitCode == 124) return "Compilation time limit exceeded.";
        if (exitCode == 137) return "Compilation was terminated before it completed.";
        return null;
    }
}
