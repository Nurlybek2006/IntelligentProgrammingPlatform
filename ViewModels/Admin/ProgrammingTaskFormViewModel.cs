using System.ComponentModel.DataAnnotations;
using IntelligentProgrammingPlatform.Models.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public class ProgrammingTaskFormViewModel
{
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [RegularExpression("^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$",
        ErrorMessage = "Use letters, numbers, and single hyphens between words.")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(Difficulty))]
    public Difficulty Difficulty { get; set; }

    [Range(1, int.MaxValue), Display(Name = "Topic")]
    public int TopicId { get; set; }

    [Range(100, 30000), Display(Name = "Time limit (ms)")]
    public int TimeLimitMs { get; set; } = 2000;

    [Range(16, 1024), Display(Name = "Memory limit (MB)")]
    public int MemoryLimitMb { get; set; } = 256;

    [Display(Name = "Published")]
    public bool IsPublished { get; set; }

    [ValidateNever]
    public List<SelectListItem> Topics { get; set; } = new();
}
