using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public class TestCaseFormViewModel
{
    [Range(1, int.MaxValue)]
    public int ProgrammingTaskId { get; set; }

    // Empty strings are meaningful test data. Do not trim or require nonempty text.
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string Input { get; set; } = string.Empty;

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [Display(Name = "Expected output")]
    public string ExpectedOutput { get; set; } = string.Empty;

    [Display(Name = "Hidden")]
    public bool IsHidden { get; set; }

    [Range(0, 100000)]
    public int Order { get; set; }
}
