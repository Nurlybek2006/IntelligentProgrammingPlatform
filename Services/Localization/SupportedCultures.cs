namespace IntelligentProgrammingPlatform.Services.Localization;

public static class SupportedCultures
{
    public const string Default = "kk-KZ";
    public static readonly IReadOnlyList<string> Names = Array.AsReadOnly(new[] { Default, "ru-RU", "en-US" });

    // Тек платформаның үш нақты culture атауын қабылдайды.
    public static bool Contains(string? culture) => Names.Contains(culture, StringComparer.Ordinal);
}
