using System;
using System.IO;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IProcessRunner _runner;
    public MaintenanceService(IProcessRunner runner) => _runner = runner;

    public Task<int> RunWingetUpgradeAsync()
        => _runner.RunAsync("winget", "upgrade --all --include-unknown --accept-package-agreements --accept-source-agreements",
            onOutput: s => Log("winget", s), onError: s => Log("winget!", s), elevate: true);

    public async Task<int> RunWindowsUpdateAsync()
    {
        // Try UsoClient chain (scan->download->install)
        var scan    = await _runner.RunAsync("UsoClient.exe", "StartScan",    s=>Log("wu", s), s=>Log("wu!", s), elevate:true);
        var dl      = await _runner.RunAsync("UsoClient.exe", "StartDownload", s=>Log("wu", s), s=>Log("wu!", s), elevate:true);
        var install = await _runner.RunAsync("UsoClient.exe", "StartInstall",  s=>Log("wu", s), s=>Log("wu!", s), elevate:true);
        return scan|dl|install;
    }

    public async Task<int> RunDefenderUpdateAndQuickScanAsync()
    {
        var mp = ResolveMpCmdRun();
        if (mp == null) return -1;
        var upd = await _runner.RunAsync(mp, "-SignatureUpdate", s=>Log("def", s), s=>Log("def!", s), elevate:true);
        var scan= await _runner.RunAsync(mp, "-Scan -ScanType 1",  s=>Log("def", s), s=>Log("def!", s), elevate:true);
        return upd|scan;
    }

    private static string? ResolveMpCmdRun()
    {
        var paths = new[]{
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "Platform", "MpCmdRun.exe"),
            "MpCmdRun.exe"
        };
        foreach (var p in paths) if (File.Exists(p)) return p;
        return null;
    }

    private static void Log(string tag, string line) => System.Diagnostics.Debug.WriteLine($"[{tag}] {line}");
}
