using System;

namespace Virgil.Services.Assistant;

public sealed record LlamaRuntimeDiagnostics(
    string ExecutablePath,
    string Arguments,
    string CommandLine,
    string SecurityFlagsDetected,
    string SecurityStrategy,
    string Stdout,
    string Stderr,
    int? ExitCode,
    bool ProcessLaunched,
    bool PortOpen,
    string? LastErrorMessage)
{
    public static LlamaRuntimeDiagnostics Empty { get; } = new(
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
        null);
}

public static class LlamaRuntimeDiagnosticsStore
{
    private static readonly object SyncRoot = new();
    private static LlamaRuntimeDiagnostics _latest = LlamaRuntimeDiagnostics.Empty;

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
        lock (SyncRoot)
        {
            _latest = diagnostics;
        }
    }

    public static void Update(Func<LlamaRuntimeDiagnostics, LlamaRuntimeDiagnostics> update)
    {
        if (update is null)
        {
            return;
        }

        lock (SyncRoot)
        {
            _latest = update(_latest);
        }
    }
}
