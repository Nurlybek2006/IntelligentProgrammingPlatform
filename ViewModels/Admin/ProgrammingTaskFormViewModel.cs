using System.ComponentModel.DataAnnotations;
using IntelligentProgrammingPlatform.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public class ProgrammingTaskFormViewModel
{
    [Required(ErrorMessage = "Validation_Required"), StringLength(200, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), StringLength(200, ErrorMessage = "Validation_StringLength")]
    [Display(Name = "Field_Slug")]
    [RegularExpression("^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$",
        ErrorMessage = "Validation_Slug")]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_Required"), Display(Name = "Field_Description")]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(Difficulty), ErrorMessage = "Validation_Difficulty"), Display(Name = "Field_Difficulty")]
    public Difficulty Difficulty { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Validation_Topic"), Display(Name = "Field_Topic")]
    public int TopicId { get; set; }

    [Range(100, 30000, ErrorMessage = "Validation_Range"), Display(Name = "Field_TimeLimit")]
    public int TimeLimitMs { get; set; } = 2000;

    [Range(16, 1024, ErrorMessage = "Validation_Range"), Display(Name = "Field_MemoryLimit")]
    public int MemoryLimitMb { get; set; } = 256;

    [Display(Name = "Field_Published")]
    public bool IsPublished { get; set; }

    [ValidateNever]
    public List<SelectListItem> Topics { get; set; } = new();
}
