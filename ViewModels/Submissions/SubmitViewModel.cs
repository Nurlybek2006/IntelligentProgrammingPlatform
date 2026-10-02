using System.ComponentModel.DataAnnotations;
using System.Text;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public class SubmitViewModel : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Validation_Range"), Display(Name = "Field_Task")]
    public int ProgrammingTaskId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Validation_Range"), Display(Name = "Field_Runtime")]
    public int RuntimeId { get; set; }

    [Required(ErrorMessage = "Validation_Required"), StringLength(CodeRunnerOptions.MaxSourceBytes, ErrorMessage = "Validation_SourceLimit")]
    [Display(Name = "Field_SourceCode")]
    public string SourceCode { get; set; } = string.Empty;

    [ValidateNever]
    public List<SelectListItem> Runtimes { get; set; } = new();

    public const string StarterCode = "#include <iostream>\nusing namespace std;\n\nint main()\n{\n    // Write your solution here\n\n    return 0;\n}\n";

    // Код көлемін таңбамен ғана емес, UTF-8 байттарымен де тексереді.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Encoding.UTF8.GetByteCount(SourceCode ?? string.Empty) > CodeRunnerOptions.MaxSourceBytes)
            yield return new ValidationResult(validationContext.GetRequiredService<IStringLocalizer<SharedResource>>()["Validation_SourceLimit"], new[] { nameof(SourceCode) });
    }
}
