using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace IntelligentProgrammingPlatform.Services.AI;

public sealed class AiFeedbackContent
{
    public required string Summary { get; init; }
    public required string ErrorCategory { get; init; }
    public required string Explanation { get; init; }
    public required string[] Hints { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    // AI JSON-ын қатаң десериализациялап, өріс шектерін және дайын бағдарлама белгілерін тексереді.
    public static AiFeedbackContent Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > 16 * 1024)
            throw new InvalidDataException("AI response exceeds the supported size.");
        var content = JsonSerializer.Deserialize<AiFeedbackContent>(json, JsonOptions)
            ?? throw new InvalidDataException("AI response is empty.");
        ValidateText(content.Summary, 600);
        ValidateText(content.Explanation, 3000);
        if (content.Hints == null || content.Hints.Length is < 1 or > 3)
            throw new InvalidDataException("AI hint count is invalid.");
        foreach (var hint in content.Hints) ValidateText(hint, 300);
        if (content.ErrorCategory is not ("Compilation" or "Logic" or "Runtime" or "TimeLimit"
            or "Memory" or "OutputFormat" or "Unknown"))
            throw new InvalidDataException("AI error category is invalid.");
        return content;
    }

    // Бос не тым ұзын мәтінді және көшіруге дайын C++ блоктарын қабылдамайды.
    private static void ValidateText(string? text, int limit)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > limit || text.Contains("```")
            || Regex.IsMatch(text, @"#\s*include|\b(?:int|auto|void)\s+main\s*\([^)]*\)\s*\{", RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)))
            throw new InvalidDataException("AI feedback is not bounded tutoring text.");
    }
}

public enum AiAnalysisStatus { Saved, Existing, NotFound, NotFinished, NotConfigured, Busy, Unavailable }
public sealed record AiAnalysisResult(AiAnalysisStatus Status);
