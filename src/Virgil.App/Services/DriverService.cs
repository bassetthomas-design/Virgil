using System;
using System.IO;
using System.Management;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public class DriverService : IDriverService
{
    private readonly IProcessRunner _runner;
    public DriverService(IProcessRunner runner) => _runner = runner;

    public Task<int> BackupDriversAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);
        // Use verbatim string for clearer quoting of the output dir
        var args = $@"/export-driver * "{outDir}"";
        return _runner.RunAsync("pnputil.exe", args, elevate:true);
    }

    public async Task<int> ScanAndUpdateDriversAsync()
    {
        var wuScan = await _runner.RunAsync("UsoClient.exe", "StartScan", elevate:true);
        var wuInstall = await _runner.RunAsync("UsoClient.exe", "StartInstall", elevate:true);
        var scan = await _runner.RunAsync("pnputil.exe", "/scan-devices", elevate:true);
        return wuScan | wuInstall | scan;
    }
}
