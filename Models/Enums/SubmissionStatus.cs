namespace IntelligentProgrammingPlatform.Models.Enums
{
    public enum SubmissionStatus
    {
        Pending = 0,
        Compiling = 1,
        Running = 2,
        Accepted = 3,
        WrongAnswer = 4,
        CompilationError = 5,
        RuntimeError = 6,
        TimeLimitExceeded = 7,
        MemoryLimitExceeded = 8,
        InternalError = 9
    }
}
