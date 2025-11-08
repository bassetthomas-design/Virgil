using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Virgil.App.Services;

public class ActivityDetector : IActivityDetector
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", SetLastError=true)] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private static readonly string[] Browsers = new[]{ "chrome", "msedge", "firefox", "opera", "brave" };
    private static readonly string[] GamesHints = new[]{ "steam", "epicgameslauncher", "battle.net", "riotclient", "ubisoftconnect" };

    public ActivityKind Detect()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return ActivityKind.Idle;
            GetWindowThreadProcessId(hwnd, out var pid);
            var p = Process.GetProcessById((int)pid);
            var name = (p.ProcessName ?? string.Empty).ToLowerInvariant();
            if (Browsers.Any(b => name.Contains(b))) return ActivityKind.Web;
            if (GamesHints.Any(g => name.Contains(g))) return ActivityKind.Game;
            return ActivityKind.Work;
        }
        catch { return ActivityKind.Idle; }
    }
}
