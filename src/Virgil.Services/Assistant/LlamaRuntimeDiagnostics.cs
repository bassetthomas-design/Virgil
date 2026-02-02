using System;

namespace Virgil.Services.Assistant;

public sealed record LlamaRuntimeDiagnostics(
    string ExecutablePath,
    string Arguments,
    string CommandLine,
    string BaseUrl,
    string SecurityFlagsDetected,
    string SecurityStrategy,
    string WarningMessage,
    string Stdout,
    string Stderr,
    int? ExitCode,
    bool ProcessRunning,
    bool PortOpen,
    string? LastErrorMessage,
    int? LastReadinessHttpStatus,
    string? LastModelsResponseExcerpt,
    string? LastModelsErrorMessage,
    LocalStatus LocalStatus,
    string? LocalModelId,
    string? FailureCategory)
{
    public static LlamaRuntimeDiagnostics Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        null,
        false,
        false,
        null,
        null,
        null,
        null,
        LocalStatus.Disabled,
        null,
        null);
}

public static class LlamaRuntimeDiagnosticsStore
{
    private static readonly object SyncRoot = new();
    private static LlamaRuntimeDiagnostics _latest = LlamaRuntimeDiagnostics.Empty;

    public static event EventHandler<LlamaRuntimeDiagnostics>? DiagnosticsUpdated;

    public static LlamaRuntimeDiagnostics Latest
    {
        get
        {
            lock (SyncRoot)
            {
                return _latest;
            }
        }
    }

    public static void Set(LlamaRuntimeDiagnostics diagnostics)
    {
        if (diagnostics is null)
        {
            return;
        }

        LlamaRuntimeDiagnostics latest;
        lock (SyncRoot)
        {
            _latest = diagnostics;
            latest = _latest;
        }

        LocalLlamaStateService.Instance.UpdateFromDiagnostics(latest);
        DiagnosticsUpdated?.Invoke(null, latest);
    }

    public static void Update(Func<LlamaRuntimeDiagnostics, LlamaRuntimeDiagnostics> update)
    {
        if (update is null)
        {
            return;
        }

        LlamaRuntimeDiagnostics latest;
        lock (SyncRoot)
        {
            _latest = update(_latest);
            latest = _latest;
        }

        LocalLlamaStateService.Instance.UpdateFromDiagnostics(latest);
        DiagnosticsUpdated?.Invoke(null, latest);
    }
}
