using System;

namespace Virgil.Services.Assistant;

public sealed record LocalLlamaStateSnapshot(
    LocalStatus Status,
    bool ProcessRunning,
    string BaseUrl,
    string ModelId,
    int? LastReadinessStatusCode,
    string? LastFailure,
    DateTimeOffset LastStateChangeUtc,
    DateTimeOffset? StartRequestedUtc,
    int StartAttemptCount);

public sealed class LocalLlamaStateService
{
    private static readonly object SyncRoot = new();
    private LocalLlamaStateSnapshot _snapshot = new(
        LocalStatus.Disabled,
        false,
        string.Empty,
        string.Empty,
        null,
        null,
        DateTimeOffset.UtcNow,
        null,
        0);

    public static LocalLlamaStateService Instance { get; } = new();

    public event EventHandler<LocalLlamaStateSnapshot>? StateUpdated;

    public LocalStatus Status => Snapshot.Status;
    public bool ProcessRunning => Snapshot.ProcessRunning;
    public string BaseUrl => Snapshot.BaseUrl;
    public string ModelId => Snapshot.ModelId;
    public int? LastReadinessStatusCode => Snapshot.LastReadinessStatusCode;
    public string? LastFailure => Snapshot.LastFailure;
    public DateTimeOffset LastStateChangeUtc => Snapshot.LastStateChangeUtc;
    public DateTimeOffset? StartRequestedUtc => Snapshot.StartRequestedUtc;
    public int StartAttemptCount => Snapshot.StartAttemptCount;

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

    internal void MarkReadyFromModels(string? baseUrl, string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        Update(current => Normalize(current, current with
        {
            Status = LocalStatus.Ready,
            ProcessRunning = true,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? current.BaseUrl : baseUrl,
            ModelId = modelId,
            LastReadinessStatusCode = 200,
            LastFailure = null
        }));
    }

    internal void MarkStartRequested()
    {
        var now = DateTimeOffset.UtcNow;
        Update(current =>
        {
            var proposed = current with
            {
                StartRequestedUtc = now,
                StartAttemptCount = current.StartAttemptCount + 1
            };

            if (current.Status is LocalStatus.Disabled or LocalStatus.Stopped or LocalStatus.Failed)
            {
                proposed = proposed with { Status = LocalStatus.Starting, LastFailure = null };
            }

            return Normalize(current, proposed);
        });
    }

    public void MarkDisabled()
    {
        Update(current => current with
        {
            Status = LocalStatus.Disabled,
            ProcessRunning = false,
            ModelId = string.Empty,
            LastReadinessStatusCode = null,
            LastFailure = null,
            StartRequestedUtc = null,
            StartAttemptCount = 0
        });
    }

    public void MarkFailed(string? failure)
    {
        var message = string.IsNullOrWhiteSpace(failure) ? "Erreur runtime local." : failure;
        Update(current => current with
        {
            Status = LocalStatus.Failed,
            ProcessRunning = false,
            LastFailure = message
        });
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
            var previous = _snapshot;
            var proposed = update(previous);
            if (proposed.Status != previous.Status)
            {
                proposed = proposed with { LastStateChangeUtc = DateTimeOffset.UtcNow };
            }

            _snapshot = proposed;
            latest = _snapshot;
        }

        StateUpdated?.Invoke(this, latest);
    }

    private static LocalLlamaStateSnapshot BuildSnapshot(LocalLlamaStateSnapshot current, LlamaRuntimeDiagnostics diagnostics)
    {
        var baseUrl = string.IsNullOrWhiteSpace(diagnostics.BaseUrl) ? current.BaseUrl : diagnostics.BaseUrl;
        var modelId = string.IsNullOrWhiteSpace(diagnostics.LocalModelId) ? current.ModelId : diagnostics.LocalModelId;
        var lastReadinessStatus = diagnostics.LastReadinessHttpStatus ?? current.LastReadinessStatusCode;
        var failure = ResolveFailure(current, diagnostics.LocalStatus, diagnostics.ProcessRunning, diagnostics.LastErrorMessage);

        return new LocalLlamaStateSnapshot(
            diagnostics.LocalStatus,
            diagnostics.ProcessRunning,
            baseUrl,
            modelId,
            lastReadinessStatus,
            failure,
            current.LastStateChangeUtc,
            current.StartRequestedUtc,
            current.StartAttemptCount);
    }

    private static string? ResolveFailure(
        LocalLlamaStateSnapshot current,
        LocalStatus status,
        bool processRunning,
        string? lastErrorMessage)
    {
        if (status == LocalStatus.Ready)
        {
            return null;
        }

        if (status == LocalStatus.Disabled)
        {
            return null;
        }

        if (status == LocalStatus.Failed && !processRunning)
        {
            return string.IsNullOrWhiteSpace(lastErrorMessage)
                ? "Erreur runtime local."
                : lastErrorMessage;
        }

        return current.LastFailure;
    }

    private static LocalLlamaStateSnapshot Normalize(LocalLlamaStateSnapshot current, LocalLlamaStateSnapshot proposed)
    {
        if (current.Status == LocalStatus.Disabled && proposed.Status == LocalStatus.Stopped)
        {
            proposed = proposed with { Status = LocalStatus.Disabled, ProcessRunning = false };
        }

        var hasModelsReady = proposed.LastReadinessStatusCode == 200
            && (proposed.ProcessRunning || current.ProcessRunning)
            && proposed.Status is not (LocalStatus.Stopped or LocalStatus.Disabled);
        if (hasModelsReady)
        {
            proposed = proposed with
            {
                Status = LocalStatus.Ready,
                ProcessRunning = true,
                LastFailure = null
            };
        }

        if ((current.ProcessRunning || proposed.ProcessRunning) && proposed.Status == LocalStatus.Failed)
        {
            var fallbackStatus = current.Status is LocalStatus.Stopped or LocalStatus.Disabled
                ? LocalStatus.Starting
                : current.Status;
            proposed = proposed with
            {
                Status = fallbackStatus,
                LastFailure = current.LastFailure
            };
        }

        if (proposed.Status == LocalStatus.Ready)
        {
            return proposed with { ProcessRunning = true, LastFailure = null };
        }

        if (proposed.Status == LocalStatus.Disabled)
        {
            return proposed with { ProcessRunning = false, LastFailure = null };
        }

        return proposed;
    }
}
