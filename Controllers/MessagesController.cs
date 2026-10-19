using System.Security.Claims;
using IntelligentProgrammingPlatform.Services.Social;
using IntelligentProgrammingPlatform.ViewModels.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Controllers;

[Authorize, ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class MessagesController : Controller
{
    private readonly MessagingService _messages;
    private readonly IStringLocalizer<SharedResource> _text;
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Контроллерге тек қатысушының хаттарын басқаратын қызмет пен аударманы береді.
    public MessagesController(MessagingService messages, IStringLocalizer<SharedResource> text) { _messages = messages; _text = text; }

    [HttpGet]
    // Ағымдағы аккаунттың жиырмадан беттелген диалогтарын көрсетеді.
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(new MessagesViewModel { Conversations = await _messages.GetListAsync(ActorId, page, cancellationToken) });

    [HttpGet]
    // Тек қатысушыға диалогтың шектелген тарихын ашады; GET оқылған күйді өзгертпейді.
    public async Task<IActionResult> Conversation(long id, long? beforeId, CancellationToken cancellationToken)
    {
        var thread = await _messages.GetThreadAsync(ActorId, id, beforeId, cancellationToken);
        return thread == null ? NotFound() : View("Index", new MessagesViewModel
            { Conversations = await _messages.GetListAsync(ActorId, 1, cancellationToken), Thread = thread });
    }

    [HttpGet]
    // Real-time оқиғасынан кейін де SQL қатысушы сүзгісі арқылы соңғы хаттарды қайта оқиды.
    public async Task<IActionResult> ThreadData(long id, CancellationToken cancellationToken)
    {
        var thread = await _messages.GetThreadAsync(ActorId, id, null, cancellationToken);
        return thread == null ? NotFound() : Json(thread);
    }

    [HttpGet]
    // Жеке диалог preview-лері мен unread сандарын тек иесіне қайтарады.
    public async Task<IActionResult> ListData(int page = 1, CancellationToken cancellationToken = default) =>
        Json(await _messages.GetListAsync(ActorId, page, cancellationToken));

    [HttpPost, EnableRateLimiting(SocialRateLimits.Actions)]
    // Message батырмасының POST сұрауымен ғана диалог ашады немесе бұрынғысын қолданады.
    public async Task<IActionResult> Open(string targetUserId, CancellationToken cancellationToken)
    {
        var result = await _messages.OpenAsync(ActorId, targetUserId, cancellationToken);
        return result.Succeeded ? RedirectToAction(nameof(Conversation), new { id = result.Id }) : SocialResponses.Failure(this, _text, result.Outcome);
    }

    [HttpPost, EnableRateLimiting(SocialRateLimits.Messages), RequestSizeLimit(32768)]
    [RequestFormLimits(ValueLengthLimit = 8000, ValueCountLimit = 8)]
    // Antiforgery тексерілген форманың мәтінін қызметке беріп, клиент жіберген иені қабылдамайды.
    public async Task<IActionResult> Send(SendDirectMessageViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return SocialResponses.Failure(this, _text, SocialOutcome.InvalidMessage);
        var result = await _messages.SendAsync(ActorId, model.ConversationId, model.Body, model.ClientMessageId, cancellationToken);
        if (!result.Succeeded) return SocialResponses.Failure(this, _text, result.Outcome);
        return SocialResponses.WantsJson(Request) ? Json(new { id = result.Id, conversationId = model.ConversationId })
            : RedirectToAction(nameof(Conversation), new { id = model.ConversationId });
    }

    [HttpPost]
    // Өз диалогындағы алушыға тиесілі кіріс хаттарын ғана оқылған деп белгілейді.
    public async Task<IActionResult> Read(long conversationId, long throughId, CancellationToken cancellationToken)
    {
        var result = await _messages.MarkReadAsync(ActorId, conversationId, throughId, cancellationToken);
        if (!result.Succeeded) return SocialResponses.Failure(this, _text, result.Outcome);
        return SocialResponses.WantsJson(Request) ? Json(new { success = true }) : RedirectToAction(nameof(Conversation), new { id = conversationId });
    }
}
