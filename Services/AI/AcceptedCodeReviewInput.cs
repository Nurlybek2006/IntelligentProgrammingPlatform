using System.Text.Json.Serialization;

namespace IntelligentProgrammingPlatform.Services.AI;

// Тесттер, аккаунт өрістері және сервер конфигурациясы бұл DTO-ға кірмейді.
public sealed record AcceptedCodeReviewInput(string TaskTitle, string TaskDescription, string Difficulty,
    string Language, string SourceCode)
{
    [JsonIgnore]
    public AiResponseLanguage ResponseLanguage { get; init; } = AiResponseLanguage.Kazakh;
}

public interface IAcceptedCodeReviewClient
{
    // Accepted кодына ғана арналған шектеулі құрылымды шолуды алады.
    Task<AiModelResponse> ReviewAsync(AcceptedCodeReviewInput input, CancellationToken cancellationToken);
}

public enum AcceptedCodeReviewStatus { Saved, Existing, NotFound, NotAccepted, NotConfigured, Busy, Unavailable }
