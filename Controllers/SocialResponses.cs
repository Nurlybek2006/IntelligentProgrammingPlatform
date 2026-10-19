using IntelligentProgrammingPlatform.Services.Social;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Controllers;

internal static class SocialResponses
{
    // AJAX және кәдімгі форма жауабының түрін Accept тақырыбы арқылы таңдайды.
    public static bool WantsJson(HttpRequest request) => request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

    // SQL немесе hub ішкі мәліметін ашпай, бірдей локализацияланған қате қайтарады.
    public static IActionResult Failure(Controller controller, IStringLocalizer<SharedResource> text, SocialOutcome outcome)
    {
        var code = outcome switch { SocialOutcome.NotFound => 404, SocialOutcome.NotAllowed or SocialOutcome.Unavailable => 403,
            SocialOutcome.InvalidMessage => 400, SocialOutcome.Busy => 409, _ => 503 };
        var error = text["Social_Result_" + outcome].Value;
        return WantsJson(controller.Request) ? new ObjectResult(new { error }) { StatusCode = code }
            : new ContentResult { Content = error, ContentType = "text/plain; charset=utf-8", StatusCode = code };
    }
}
