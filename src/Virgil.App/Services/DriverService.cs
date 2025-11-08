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
        return _runner.RunAsync("pnputil.exe", $"/export-driver * ""{outDir}""", elevate:true);
    }

    public async Task<int> ScanAndUpdateDriversAsync()
    {
        // Best effort: tenter Windows Update drivers via UsoClient, puis pnputil scan
        var wuScan = await _runner.RunAsync("UsoClient.exe", "StartScan", elevate:true);
        var wuInstall = await _runner.RunAsync("UsoClient.exe", "StartInstall", elevate:true);
        var scan = await _runner.RunAsync("pnputil.exe", "/scan-devices", elevate:true);
        return wuScan | wuInstall | scan;
    }
}
