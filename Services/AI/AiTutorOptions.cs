namespace IntelligentProgrammingPlatform.Services.AI;

public sealed class AiTutorOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-6-luna";
    public int TimeoutSeconds { get; set; } = 45;
    public int MaxOutputTokens { get; set; } = 1800;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(Model) && Model.Length <= 100;
}
