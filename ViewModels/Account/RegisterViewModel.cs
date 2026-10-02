using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Validation_Required"), StringLength(100, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), EmailAddress(ErrorMessage = "Validation_Email"), StringLength(256, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), StringLength(100, MinimumLength = 8, ErrorMessage = "Validation_PasswordLength"), DataType(DataType.Password)]
    [Display(Name = "Field_Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Validation_Compare")]
    [Display(Name = "Field_ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
