using IntelligentProgrammingPlatform.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntelligentProgrammingPlatform.ViewModels.Tasks;

public class TaskCatalogViewModel
{
    public string? Search { get; set; }
    public int? TopicId { get; set; }
    public Difficulty? Difficulty { get; set; }
    public List<SelectListItem> Topics { get; set; } = new();
    public List<TaskListItemViewModel> Tasks { get; set; } = new();
}

public class TaskListItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public Difficulty Difficulty { get; set; }
}
