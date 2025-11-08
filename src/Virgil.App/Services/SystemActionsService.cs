using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public class SystemActionsService : ISystemActionsService
{
    [Flags] private enum RecycleFlags : int { SHERB_NOCONFIRMATION=1, SHERB_NOPROGRESSUI=2, SHERB_NOSOUND=4 }
    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);

    private readonly IProcessRunner _runner = new ProcessRunner();

    // Déjà sur main
    public Task<int> WsResetAsync() => _runner.RunAsync("wsreset.exe", string.Empty, elevate:true);

    public Task<int> RebuildExplorerCachesAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var explorer = Path.Combine(local, "Microsoft", "Windows", "Explorer");
                if (Directory.Exists(explorer))
                {
                    foreach (var file in Directory.GetFiles(explorer, "thumbcache*")) { try { File.Delete(file); } catch {} }
                    foreach (var file in Directory.GetFiles(explorer, "iconcache*"))  { try { File.Delete(file); } catch {} }
                }
                return 0;
            }
            catch { return 1; }
        });
    }

    public Task<int> EmptyRecycleBinAsync()
    {
        return Task.Run(() =>
        {
            try { return SHEmptyRecycleBin(IntPtr.Zero, null!, RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND); }
            catch { return 1; }
        });
    }

    // Ajouts Rambo
    public Task<int> RestartExplorerAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("explorer"))
                {
                    try { p.Kill(true); p.WaitForExit(5000); } catch {}
                }
                var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                Process.Start(psi);
                return 0;
            }
            catch { return 1; }
        });
    }

    public async Task<int> RebuildExplorerCachesAndRestartAsync()
    {
        var a = await RebuildExplorerCachesAsync();
        var b = await RestartExplorerAsync();
        return (a==0 && b==0) ? 0 : 1;
    }
}
