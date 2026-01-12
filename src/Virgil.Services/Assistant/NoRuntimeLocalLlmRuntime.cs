using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class NoRuntimeLocalLlmRuntime : ILocalLlmRuntime
{
    public NoRuntimeLocalLlmRuntime(string runtimePathUsed)
    {
        RuntimePathUsed = runtimePathUsed;
    }

    public bool IsRuntimeAvailable() => false;

    public string RuntimePathUsed { get; }

    public Task StartAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
