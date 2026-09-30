using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public class TopicFormViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
}
