namespace IntelligentProgrammingPlatform.Services.Security;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    // Келесі middleware-ді сақтайды.
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    // Razor орындалғаннан кейін CSP мен өзге қорғаныс тақырыптарын бір рет жазады.
    public Task InvokeAsync(HttpContext context, ContentSecurityPolicy policy)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = policy.BuildHeader();
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        return _next(context);
    }
}
