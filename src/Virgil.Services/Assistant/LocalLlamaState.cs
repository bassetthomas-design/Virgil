using System;

namespace Virgil.Services.Assistant;

public sealed record LocalLlamaFailure(string Cause, string Details);

public sealed record LocalLlamaStateSnapshot(
    LocalStatus LocalStatus,
    bool ProcessRunning,
    string BaseUrl,
    string ModelId,
    int? LastReadinessStatusCode,
    LocalLlamaFailure? LastFailure);

public sealed class LocalLlamaState
{
    private static readonly object SyncRoot = new();
    private LocalLlamaStateSnapshot _snapshot = new(
        LocalStatus.Stopped,
        false,
        string.Empty,
        string.Empty,
        null,
        null);

    public static LocalLlamaState Instance { get; } = new();

    public event EventHandler<LocalLlamaStateSnapshot>? StateUpdated;

    public LocalStatus LocalStatus => Snapshot.LocalStatus;
    public bool ProcessRunning => Snapshot.ProcessRunning;
    public string BaseUrl => Snapshot.BaseUrl;
    public string ModelId => Snapshot.ModelId;
    public int? LastReadinessStatusCode => Snapshot.LastReadinessStatusCode;
    public LocalLlamaFailure? LastFailure => Snapshot.LastFailure;

    public LocalLlamaStateSnapshot Snapshot
    {
        get
        {
            lock (SyncRoot)
            {
                return _snapshot;
            }
        }
    }

    internal void UpdateFromDiagnostics(LlamaRuntimeDiagnostics diagnostics)
    {
        if (diagnostics is null)
        {
            return;
        }

        Update(current => Normalize(current, BuildSnapshot(current, diagnostics)));
    }

    internal void UpdateModelId(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        Update(current => current with { ModelId = modelId });
    }

    private void Update(Func<LocalLlamaStateSnapshot, LocalLlamaStateSnapshot> update)
    {
        if (update is null)
        {
            return;
        }

        LocalLlamaStateSnapshot latest;
        lock (SyncRoot)
        {
            _snapshot = update(_snapshot);
            latest = _snapshot;
        }

        StateUpdated?.Invoke(this, latest);
    }

    private static LocalLlamaStateSnapshot BuildSnapshot(LocalLlamaStateSnapshot current, LlamaRuntimeDiagnostics diagnostics)
    {
        var baseUrl = string.IsNullOrWhiteSpace(diagnostics.BaseUrl) ? current.BaseUrl : diagnostics.BaseUrl;
        var modelId = string.IsNullOrWhiteSpace(diagnostics.LocalModelId) ? current.ModelId : diagnostics.LocalModelId;
        var lastReadinessStatus = diagnostics.LastReadinessHttpStatus ?? current.LastReadinessStatusCode;
        var failure = ResolveFailure(current, diagnostics.LocalStatus, diagnostics.FailureCategory, diagnostics.LastErrorMessage);

        return new LocalLlamaStateSnapshot(
            diagnostics.LocalStatus,
            diagnostics.ProcessRunning,
            baseUrl,
            modelId,
            lastReadinessStatus,
            failure);
    }

    private static LocalLlamaFailure? ResolveFailure(
        LocalLlamaStateSnapshot current,
        LocalStatus status,
        string? failureCategory,
        string? lastErrorMessage)
    {
        if (status == LocalStatus.Ready)
        {
            return null;
        }

        if (status == LocalStatus.Failed)
        {
            var cause = string.IsNullOrWhiteSpace(failureCategory) ? "Unknown" : failureCategory;
            var details = string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "Erreur runtime local."
                : lastErrorMessage;
            return new LocalLlamaFailure(cause, details);
        }

        return current.LastFailure;
    }

    private static LocalLlamaStateSnapshot Normalize(LocalLlamaStateSnapshot current, LocalLlamaStateSnapshot proposed)
    {
        if (current.LocalStatus == LocalStatus.Ready
            && proposed.LocalStatus == LocalStatus.Failed
            && (current.ProcessRunning || proposed.ProcessRunning))
        {
            return proposed with
            {
                LocalStatus = LocalStatus.Ready,
                LastFailure = current.LastFailure
            };
        }

        if (proposed.LocalStatus == LocalStatus.Ready)
        {
            return proposed with { LastFailure = null };
        }

        return proposed;
    }
}
