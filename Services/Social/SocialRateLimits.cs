using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace IntelligentProgrammingPlatform.Services.Social;

public static class SocialRateLimits
{
    public const string Messages = "social-messages";
    public const string Requests = "social-requests";
    public const string Actions = "social-actions";

    // Бір аккаунттың хабарлама, достық сұрауы және өзге әрекеттеріне бөлек минуттық лимит қояды.
    public static void Configure(RateLimiterOptions options)
    {
        options.AddPolicy(Messages, context => Partition(context, 30));
        options.AddPolicy(Requests, context => Partition(context, 10));
        options.AddPolicy(Actions, context => Partition(context, 30));
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            var http = context.HttpContext;
            var text = http.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
            var error = text[http.Request.Path.StartsWithSegments("/Messages/Send") ? "Social_TooManyMessages" : "Social_TooManyRequests"].Value;
            http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            http.Response.Headers.RetryAfter = "60";
            http.Response.Headers.CacheControl = "no-store";
            if (http.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
                await http.Response.WriteAsJsonAsync(new { error }, cancellationToken);
            else { http.Response.ContentType = "text/plain; charset=utf-8"; await http.Response.WriteAsync(error, cancellationToken); }
        };
    }

    // Лимит кілтін браузер өрісінен емес, тек аутентификацияланған Identity claim-нен алады.
    private static RateLimitPartition<string> Partition(HttpContext context, int permits) =>
        RateLimitPartition.GetSlidingWindowLimiter(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
            _ => new SlidingWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6,
                QueueLimit = 0, AutoReplenishment = true });
}
