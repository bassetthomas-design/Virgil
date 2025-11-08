using System.Threading.Tasks;

namespace Virgil.App.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IProcessRunner _runner;
    public MaintenanceService(IProcessRunner runner) => _runner = runner;

    public Task<int> RunWingetUpgradeAsync() => _runner.RunAsync("winget", "upgrade --all --include-unknown --accept-package-agreements --accept-source-agreements", elevate:true);
    public async Task<int> RunWindowsUpdateAsync()
    {
        var a = await _runner.RunAsync("UsoClient.exe", "StartScan", elevate:true);
        var b = await _runner.RunAsync("UsoClient.exe", "StartDownload", elevate:true);
        var c = await _runner.RunAsync("UsoClient.exe", "StartInstall", elevate:true);
        return a | b | c;
    }
    public async Task<int> RunDefenderUpdateAndQuickScanAsync()
    {
        var a = await _runner.RunAsync("MpCmdRun.exe", "-SignatureUpdate", elevate:true);
        var b = await _runner.RunAsync("MpCmdRun.exe", "-Scan -ScanType 1", elevate:true);
        return a | b;
    }
    public Task<int> RunDismComponentCleanupAsync()
        => _runner.RunAsync("Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /Quiet", elevate:true);
}
