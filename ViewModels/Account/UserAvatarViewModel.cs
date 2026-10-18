using System.Globalization;
using IntelligentProgrammingPlatform.Services.Profile;

namespace IntelligentProgrammingPlatform.ViewModels.Account;

public sealed class UserAvatarViewModel
{
    public string? DisplayName { get; init; }
    public string? AvatarPath { get; init; }
    public bool Large { get; init; }
    public bool Small { get; init; }
    public string? SafePath => AvatarService.IsManagedPath(AvatarPath) ? AvatarPath : null;
    public string Initials => GetInitials(DisplayName);

    // Бос орындарды өткізіп, бірінші және соңғы есімнің Unicode таңбасын алады.
    public static string GetInitials(string? name)
    {
        var parts = (name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        var first = StringInfo.GetNextTextElement(parts[0]);
        var last = parts.Length > 1 ? StringInfo.GetNextTextElement(parts[^1]) : "";
        return (first + last).ToUpperInvariant();
    }
}
