using System.ComponentModel.DataAnnotations;

namespace IntelligentProgrammingPlatform.ViewModels.Admin;

public class TestCaseFormViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Validation_Task"), Display(Name = "Field_Task")]
    public int ProgrammingTaskId { get; set; }

    // Empty strings are meaningful test data. Do not trim or require nonempty text.
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [Display(Name = "Field_Input")]
    public string Input { get; set; } = string.Empty;

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [Display(Name = "Field_ExpectedOutput")]
    public string ExpectedOutput { get; set; } = string.Empty;

    [Display(Name = "Field_Hidden")]
    public bool IsHidden { get; set; }

    [Range(0, 100000, ErrorMessage = "Validation_Range"), Display(Name = "Field_Order")]
    public int Order { get; set; }
}
