using System.Text.Json.Serialization;

namespace IntelligentProgrammingPlatform.Services.AI;

public enum AiResponseLanguage { Kazakh, Russian, English }

// Бұл DTO әдейі Identity, database ID және hidden тест мәтіндерін қамтымайды.
public sealed record AiTutorInput(string TaskTitle, string TaskDescription, string Difficulty,
    string Language, string SourceCode, string SubmissionStatus, string? CompilerOutput,
    int PassedTests, int TotalTests, IReadOnlyList<AiVisibleTest> VisibleFailedTests,
    IReadOnlyList<AiHiddenTest> HiddenTests)
{
    // Сервер таңдаған тіл студенттің сенімсіз JSON деректеріне қосылмайды.
    [JsonIgnore]
    public AiResponseLanguage ResponseLanguage { get; init; } = AiResponseLanguage.Kazakh;
}

public sealed record AiVisibleTest(int Number, string Status, string Input,
    string ExpectedOutput, string ActualOutput, string? ErrorMessage);

public sealed record AiHiddenTest(int Number, string Status, int? ExecutionTimeMs);

public sealed record AiModelResponse(string Json, string Model, int? InputTokens, int? OutputTokens);

public interface IAiFeedbackClient
{
    // Қауіпсіз DTO-дан шектеулі құрылымды кеңес алады; тестте ақылы API орнына fake қолданылады.
    Task<AiModelResponse> GenerateAsync(AiTutorInput input, CancellationToken cancellationToken);
}
