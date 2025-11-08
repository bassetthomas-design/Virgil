using System;

namespace Virgil.App.Services;

// Historical interface kept for compatibility; uses the shared ActivityKind enum.
public interface IActivityDetector
{
    ActivityKind Current { get; }
    event EventHandler<ActivityKind>? ActivityChanged;
    void Start();
    void Stop();
    void NotifyInput();
}
