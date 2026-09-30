namespace IntelligentProgrammingPlatform.Services.AI;

public sealed class AiRequestGate
{
    private readonly object _sync = new();
    private readonly HashSet<long> _active = new();
    private readonly Dictionary<string, DateTimeOffset> _cooldowns = new();
    private readonly TimeProvider _clock;

    // Қатар орындалу мен қысқа пайдаланушы cooldown уақытын бір уақыт көзіне байланыстырады.
    public AiRequestGate(TimeProvider clock) => _clock = clock;

    // Екі сұраудан артық, бір submission-ға қатар немесе тым жиі AI шақыруды өткізбейді.
    public bool TryEnter(long submissionId, string userId)
    {
        lock (_sync)
        {
            var now = _clock.GetUtcNow();
            foreach (var expired in _cooldowns.Where(item => item.Value <= now).Select(item => item.Key).ToList())
                _cooldowns.Remove(expired);
            if (_active.Count >= 2 || _active.Contains(submissionId) || _cooldowns.ContainsKey(userId)) return false;
            _active.Add(submissionId);
            _cooldowns[userId] = now.AddSeconds(30);
            return true;
        }
    }

    // Аяқталған AI сұрауының белсенді орнын босатады.
    public void Exit(long submissionId)
    {
        lock (_sync) _active.Remove(submissionId);
    }
}
