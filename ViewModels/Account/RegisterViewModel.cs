using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Account;

public class RegisterViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
