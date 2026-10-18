using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Account;

public sealed class ProfileEditViewModel
{
    [Required(ErrorMessage = "Validation_Required"), StringLength(100, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_DisplayName")]
    public string DisplayName { get; set; } = string.Empty;
    public IFormFile? Avatar { get; set; }
}
