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
            BaseUrl = string.Empty,
            ProcessRunning = false,
            PortOpen = false,
            LocalStatus = LocalStatus.Disabled
        };
        LlamaRuntimeDiagnosticsStore.Set(Diagnostics);
    }

    public bool IsRuntimeAvailable() => false;

    public string RuntimePathUsed { get; }

    public LlamaRuntimeDiagnostics Diagnostics { get; }

    public string? ApiKey => null;

    public void SetModelPath(string modelPath)
    {
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        LocalLlamaStateService.Instance.MarkStartRequested();
        var message = $"Llama runtime introuvable: '{RuntimePathUsed}'.";
        LlamaRuntimeDiagnosticsStore.Update(existing => existing with
        {
            ProcessRunning = false,
            PortOpen = false,
            ExitCode = null,
            LastErrorMessage = message,
            LocalStatus = LocalStatus.Failed,
            FailureCategory = "RuntimeMissing"
        });

        LocalLlamaStateService.Instance.MarkFailed(message);
        throw new AssistantProviderUnavailableException(message);
    }

    public Task StopAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
        => Task.FromResult(false);
}
