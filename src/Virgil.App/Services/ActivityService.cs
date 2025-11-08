using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Timers;

namespace Virgil.App.Services;

public sealed class ActivityService : IActivityService, IDisposable
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private readonly Timer _timer;
    private ActivityKind _current = ActivityKind.Unknown;
    private DateTime _lastInputSeen = DateTime.UtcNow;
    private Map? _map;
    private ActivityKind _lastStable = ActivityKind.Unknown;
    private DateTime _since = DateTime.UtcNow;

    public ActivityKind Current => _current;
    public event EventHandler<ActivityKind>? ActivityChanged;

    public ActivityService()
    {
        _timer = new Timer(1000);
        _timer.Elapsed += Tick;
        LoadMap();
    }

    public void Start() => _timer.Start();
    public void Stop()  => _timer.Stop();
    public void NotifyInput() => _lastInputSeen = DateTime.UtcNow;

    private void Tick(object? s, ElapsedEventArgs e)
    {
        try
        {
            var now = DateTime.UtcNow;
            var kind = DetectForeground();

            if (kind != _lastStable)
            {
                if ((now - _since).TotalSeconds >= 3)
                {
                    _lastStable = kind;
                    Set(kind);
                }
            }
            else
            {
                _since = now;
            }
        }
        catch { }
    }

    private void Set(ActivityKind k)
    {
        if (_current == k) return;
        _current = k;
        ActivityChanged?.Invoke(this, k);
    }

    private ActivityKind DetectForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return ActivityKind.Unknown;

        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var p = Process.GetProcessById((int)pid);
            var name = (p.ProcessName + ".exe").ToLowerInvariant();

            var idleSec = _map?.IdleSeconds ?? 120;
            if ((DateTime.UtcNow - _lastInputSeen).TotalSeconds > idleSec)
                return ActivityKind.Idle;

            if (_map != null)
            {
                if (_map.Games.Contains(name))    return ActivityKind.Game;
                if (_map.Browsers.Contains(name)) return ActivityKind.Browser;
                if (_map.IDE.Contains(name))      return ActivityKind.IDE;
                if (_map.Office.Contains(name))   return ActivityKind.Office;
                if (_map.Media.Contains(name))    return ActivityKind.Media;
                if (_map.Terminal.Contains(name)) return ActivityKind.Terminal;
            }
        }
        catch { }

        return ActivityKind.Unknown;
    }

    private void LoadMap()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "assets", "activity", "process-map.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            _map = JsonSerializer.Deserialize<Map>(json);
        }
        catch { }
    }

    public void Dispose() => _timer.Dispose();

    private sealed class Map
    {
        public string[] Games { get; set; } = Array.Empty<string>();
        public string[] Browsers { get; set; } = Array.Empty<string>();
        public string[] IDE { get; set; } = Array.Empty<string>();
        public string[] Office { get; set; } = Array.Empty<string>();
        public string[] Media { get; set; } = Array.Empty<string>();
        public string[] Terminal { get; set; } = Array.Empty<string>();
        public int IdleSeconds { get; set; } = 120;
    }
}
