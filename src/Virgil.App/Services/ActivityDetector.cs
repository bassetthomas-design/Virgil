using System;
using System.Timers;

namespace Virgil.App.Services;

public class ActivityDetector : IActivityDetector
{
    private readonly IActivityService _service;
    private readonly Timer _idleTimer;

    public ActivityDetector(IActivityService service)
    {
        _service = service;
        _service.ActivityChanged += (s, kind) => ActivityChanged?.Invoke(this, kind);
        _idleTimer = new Timer(15000); // 15s idle tick to promote state if needed
        _idleTimer.Elapsed += (_, _) => ActivityChanged?.Invoke(this, _service.Current);
    }

    public ActivityKind Current => _service.Current;

    public event EventHandler<ActivityKind>? ActivityChanged;

    public void Start()
    {
        _idleTimer.Start();
        _service.Start();
    }

    public void Stop()
    {
        _idleTimer.Stop();
        _service.Stop();
    }

    public void NotifyInput()
    {
        _service.NotifyInput();
    }
}
