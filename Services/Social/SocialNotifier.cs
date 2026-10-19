using IntelligentProgrammingPlatform.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace IntelligentProgrammingPlatform.Services.Social;

public sealed class SocialNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<SocialNotifier> _logger;

    // Сақталған өзгерістерді тиісті аккаунттарға ғана жеткізетін hub контекстін қабылдайды.
    public SocialNotifier(IHubContext<ChatHub> hub, ILogger<SocialNotifier> logger) { _hub = hub; _logger = logger; }

    // Commit-тен кейін шағын оқиға жібереді; жеткізу қатесі сақталған деректі кері қайтармайды.
    public async Task NotifyAsync(IEnumerable<string> userIds, string eventName, object payload)
    {
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var groups = userIds.Distinct(StringComparer.Ordinal).Select(SocialUserGroup.For).ToArray();
            await _hub.Clients.Groups(groups).SendAsync(eventName, payload, deadline.Token);
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Social notification delivery unavailable ({FailureType})", exception.GetType().Name);
        }
    }
}
