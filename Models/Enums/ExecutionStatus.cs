namespace IntelligentProgrammingPlatform.Models.Enums
{
    public enum ExecutionStatus
    {
        Pending = 0,
        Passed = 1,
        WrongAnswer = 2,
        RuntimeError = 3,
        TimeLimitExceeded = 4,
        MemoryLimitExceeded = 5,
        Skipped = 6,
        InternalError = 7
    }
}
