using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public class SystemActionsService : ISystemActionsService
{
    [Flags] private enum RecycleFlags : int { SHERB_NOCONFIRMATION = 0x00000001, SHERB_NOPROGRESSUI = 0x00000002, SHERB_NOSOUND = 0x00000004 }
    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);

    private readonly IProcessRunner _runner = new ProcessRunner();

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
}
