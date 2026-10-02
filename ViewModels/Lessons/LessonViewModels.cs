using IntelligentProgrammingPlatform.Models.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IntelligentProgrammingPlatform.ViewModels.Lessons;

public class LessonListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int TopicId { get; set; }
    public string TopicName { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class LessonCatalogViewModel
{
    public int? TopicId { get; set; }
    public List<SelectListItem> Topics { get; set; } = new();
    public List<LessonListItemViewModel> Lessons { get; set; } = new();
}

public sealed class LessonDetailsViewModel : LessonListItemViewModel
{
    public string Content { get; set; } = string.Empty;
    public string? CodeExample { get; set; }
    public string? CodeLanguage { get; set; }
    public LessonLinkViewModel? Previous { get; set; }
    public LessonLinkViewModel? Next { get; set; }
    public List<LessonTaskViewModel> RelatedTasks { get; set; } = new();
}

public sealed class LessonLinkViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class LessonTaskViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Difficulty Difficulty { get; set; }
}
