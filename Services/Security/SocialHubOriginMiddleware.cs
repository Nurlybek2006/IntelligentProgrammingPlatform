namespace IntelligentProgrammingPlatform.Services.Security;

public sealed class SocialHubOriginMiddleware
{
    private readonly RequestDelegate _next;

    // SignalR қосылымына арналған same-origin тексеру тізбегін қабылдайды.
    public SocialHubOriginMiddleware(RequestDelegate next) => _next = next;

    // Cookie қолданатын WebSocket пен hub transport сұрауларына бөтен Origin-ді жібермейді.
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/hubs/chat"))
        {
            var origin = context.Request.Headers.Origin.ToString();
            var expected = context.Request.Scheme + "://" + context.Request.Host;
            if (context.Request.Headers["Sec-Fetch-Site"] == "cross-site"
                || (origin.Length != 0 && !string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase)))
            { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
        }
        await _next(context);
    }
}
