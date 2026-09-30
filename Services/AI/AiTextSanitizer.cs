using System.Text.RegularExpressions;

namespace IntelligentProgrammingPlatform.Services.AI;

public static class AiTextSanitizer
{
    // Мәтіндегі белгілі құпия мен host жолдарын жасырып, AI контекстінің көлемін шектейді.
    public static string Clean(string? value, int maximumLength, string? configuredKey)
    {
        var text = value ?? string.Empty;
        if (!string.IsNullOrEmpty(configuredKey)) text = text.Replace(configuredKey, "[redacted]", StringComparison.Ordinal);
        text = Regex.Replace(text, @"\bsk-[A-Za-z0-9_-]{12,}", "[redacted]", RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        text = Regex.Replace(text, @"(?:[A-Za-z]:[\\/]|/(?:home|Users|tmp|var|mnt)/)[^\s""'<>]*",
            "[path]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        const string marker = "\n[truncated]";
        return text.Length <= maximumLength ? text : text[..(maximumLength - marker.Length)] + marker;
    }
}
