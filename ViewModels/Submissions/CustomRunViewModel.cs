using System.ComponentModel.DataAnnotations;
using System.Text;
using IntelligentProgrammingPlatform.Services.CodeExecution;

namespace IntelligentProgrammingPlatform.ViewModels.Submissions;

public sealed class CustomRunViewModel : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int ProgrammingTaskId { get; set; }
    [Range(1, int.MaxValue)]
    public int RuntimeId { get; set; }
    [Required, StringLength(CodeRunnerOptions.MaxSourceBytes)]
    public string SourceCode { get; set; } = string.Empty;
    [StringLength(CodeRunnerOptions.MaxCustomInputBytes), DisplayFormat(ConvertEmptyStringToNull = false)]
    public string CustomInput { get; set; } = string.Empty;

    // Код пен custom input көлемін UTF-8 байттарымен шектейді.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Encoding.UTF8.GetByteCount(SourceCode ?? string.Empty) > CodeRunnerOptions.MaxSourceBytes)
            yield return new ValidationResult("Source code must not exceed 64 KiB (UTF-8).", new[] { nameof(SourceCode) });
        if (Encoding.UTF8.GetByteCount(CustomInput ?? string.Empty) > CodeRunnerOptions.MaxCustomInputBytes)
            yield return new ValidationResult("Custom input must not exceed 32 KiB (UTF-8).", new[] { nameof(CustomInput) });
    }
}
