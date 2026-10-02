using System.ComponentModel.DataAnnotations;
using System.Text;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public sealed class CustomRunViewModel : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Validation_Range"), Display(Name = "Field_Task")]
    public int ProgrammingTaskId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Validation_Range"), Display(Name = "Field_Runtime")]
    public int RuntimeId { get; set; }
    [Required(ErrorMessage = "Validation_Required"), StringLength(CodeRunnerOptions.MaxSourceBytes, ErrorMessage = "Validation_SourceLimit"), Display(Name = "Field_SourceCode")]
    public string SourceCode { get; set; } = string.Empty;
    [StringLength(CodeRunnerOptions.MaxCustomInputBytes, ErrorMessage = "Validation_InputLimit"), DisplayFormat(ConvertEmptyStringToNull = false), Display(Name = "Field_CustomInput")]
    public string CustomInput { get; set; } = string.Empty;

    // Код пен custom input көлемін UTF-8 байттарымен шектейді.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Encoding.UTF8.GetByteCount(SourceCode ?? string.Empty) > CodeRunnerOptions.MaxSourceBytes)
            yield return new ValidationResult(validationContext.GetRequiredService<IStringLocalizer<SharedResource>>()["Validation_SourceLimit"], new[] { nameof(SourceCode) });
        if (Encoding.UTF8.GetByteCount(CustomInput ?? string.Empty) > CodeRunnerOptions.MaxCustomInputBytes)
            yield return new ValidationResult(validationContext.GetRequiredService<IStringLocalizer<SharedResource>>()["Validation_InputLimit"], new[] { nameof(CustomInput) });
    }
}
