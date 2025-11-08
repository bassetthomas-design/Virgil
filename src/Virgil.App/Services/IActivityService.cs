using System;

namespace Virgil.App.Services;

public interface IActivityService
{
    ActivityKind Current { get; }
    event EventHandler<ActivityKind>? ActivityChanged;
    void Start();
    void Stop();
    void NotifyInput();
}
