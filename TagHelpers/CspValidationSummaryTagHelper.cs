using Microsoft.AspNetCore.Razor.TagHelpers;

namespace IntelligentProgrammingPlatform.TagHelpers;

[HtmlTargetElement("div", Attributes = "asp-validation-summary")]
public sealed class CspValidationSummaryTagHelper : TagHelper
{
    public override int Order => int.MaxValue;

    // MVC жасаған бос summary элементінің inline стилін қауіпсіз CSS класына ауыстырады.
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.PostContent.IsModified)
            output.PostContent.SetHtmlContent(output.PostContent.GetContent().Replace(
                "<li style=\"display:none\"></li>", "<li class=\"validation-summary-placeholder\"></li>",
                StringComparison.Ordinal));
    }
}
