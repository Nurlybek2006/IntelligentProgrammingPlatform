using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Validation_Required"), EmailAddress(ErrorMessage = "Validation_Email"), StringLength(256, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), DataType(DataType.Password), Display(Name = "Field_Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Field_RememberMe")]
    public bool RememberMe { get; set; }
}
