using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.ViewModels.Submissions;

namespace IntelligentProgrammingPlatform.ViewModels.Tasks;

public class TaskDetailsViewModel
{
    public int Id { get; set; }
    public AttemptJourneyViewModel? Journey { get; set; }
    public SubmitViewModel Submission { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public Difficulty Difficulty { get; set; }
    public string Description { get; set; } = string.Empty;
    public int TimeLimitMs { get; set; }
    public int MemoryLimitMb { get; set; }
    public List<TaskExampleViewModel> Examples { get; set; } = new();
}

public class TaskExampleViewModel
{
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
}
