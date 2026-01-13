using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class NoRuntimeLocalLlmRuntime : ILocalLlmRuntime
{
    public NoRuntimeLocalLlmRuntime(string runtimePathUsed)
    {
        RuntimePathUsed = runtimePathUsed;
        Diagnostics = LlamaRuntimeDiagnostics.Empty with
        {
            ExecutablePath = runtimePathUsed,
            ProcessLaunched = false,
            PortOpen = false
        };
        LlamaRuntimeDiagnosticsStore.Set(Diagnostics);
    }

    public bool IsRuntimeAvailable() => false;

    public string RuntimePathUsed { get; }

    public LlamaRuntimeDiagnostics Diagnostics { get; }

    public Task StartAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
