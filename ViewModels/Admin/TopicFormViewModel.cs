using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public class TopicFormViewModel
{
    [Required(ErrorMessage = "Validation_Required"), StringLength(100, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Validation_StringLength"), Display(Name = "Field_Description")]
    public string? Description { get; set; }
}
