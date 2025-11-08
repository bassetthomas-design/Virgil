using System;

namespace Virgil.App.Services;

// NOTE: This interface mirrors IActivityService to keep backward compatibility
// and avoids redefining the ActivityKind enum (which already exists).
public interface IActivityDetector
{
    ActivityKind Current { get; }
    event EventHandler<ActivityKind>? ActivityChanged;
    void Start();
    void Stop();
    void NotifyInput();
}
