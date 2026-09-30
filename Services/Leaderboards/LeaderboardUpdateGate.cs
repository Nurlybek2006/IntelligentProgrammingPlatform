namespace IntelligentProgrammingPlatform.Services.Leaderboards;

public sealed class LeaderboardUpdateGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1);

    // Қатар келген rebuild және жеке жаңартуды бір процесте ретімен орындайды.
    public Task EnterAsync(CancellationToken cancellationToken) => _gate.WaitAsync(cancellationToken);

    // Қысқа статистика жаңартуы аяқталғанда келесі сұрауға орын береді.
    public void Exit() => _gate.Release();

    // Қолданба тоқтағанда семафор ресурсын босатады.
    public void Dispose() => _gate.Dispose();
}
