using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public sealed class LessonFormViewModel
{
    [Required(ErrorMessage = "Validation_Required"), StringLength(200, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), StringLength(200, ErrorMessage = "Validation_StringLength")]
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Validation_LessonSlug")]
    [Display(Name = "Field_Slug")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), StringLength(1000, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_LessonSummary")]
    public string Summary { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), StringLength(50000, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_LessonContent")]
    public string Content { get; set; } = string.Empty;

    [StringLength(16000, ErrorMessage = "Validation_StringLength"), Display(Name = "Field_CodeExample")]
    public string? CodeExample { get; set; }

    [RegularExpression("^(cpp|python|text)$", ErrorMessage = "Validation_CodeLanguage")]
    [StringLength(20, ErrorMessage = "Validation_StringLength"), Display(Name = "Field_CodeLanguage")]
    public string? CodeLanguage { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Validation_Topic"), Display(Name = "Field_Topic")]
    public int TopicId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Validation_Range"), Display(Name = "Field_Order")]
    public int Order { get; set; } = 1;

    [Display(Name = "Field_Published")]
    public bool IsPublished { get; set; }

    [ValidateNever]
    public List<SelectListItem> Topics { get; set; } = new();
}
