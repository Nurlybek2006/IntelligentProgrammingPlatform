using IntelligentProgrammingPlatform.ViewModels.Account;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public sealed class UsersViewModel
{
    public int Page { get; set; }
    public bool HasNextPage { get; set; }
    public List<UserListItemViewModel> Users { get; set; } = new();
}

public sealed class UserListItemViewModel
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? AvatarPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SolvedTasks { get; set; }
    public int Score { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class UserDetailsViewModel
{
    public string Id { get; set; } = "";
    public ProfileViewModel Profile { get; set; } = new();
}
