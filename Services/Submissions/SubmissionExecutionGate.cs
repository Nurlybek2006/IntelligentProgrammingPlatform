using IntelligentProgrammingPlatform.Services.CodeExecution;

namespace IntelligentProgrammingPlatform.Services.Submissions;

public sealed class SubmissionExecutionGate : IDisposable
{
    private readonly SemaphoreSlim _slots = new(CodeRunnerOptions.MaxConcurrentSubmissions);

    // Бос орындау орнын шектеулі уақыт күтіп, қатар жүретін жіберілімдер санын шектейді.
    public Task<bool> EnterAsync(CancellationToken cancellationToken) =>
        _slots.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

    // Аяқталған жіберілімнің орындау орнын келесі сұрауға босатады.
    public void Exit() => _slots.Release();

    // Қолданба тоқтағанда семафор ресурсын босатады.
    public void Dispose() => _slots.Dispose();
}
