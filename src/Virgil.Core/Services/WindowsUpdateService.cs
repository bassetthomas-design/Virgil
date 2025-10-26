using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.Core.Services;

public sealed class WindowsUpdateService
{
    public Task<string> StartScanAsync()    => RunUsoClientAsync("StartScan");
    public Task<string> StartDownloadAsync()=> RunUsoClientAsync("StartDownload");
    public Task<string> StartInstallAsync()=> RunUsoClientAsync("StartInstall");

    private static async Task<string> RunUsoClientAsync(string cmd)
    {
        try
        {
            var psi = new ProcessStartInfo("UsoClient.exe", cmd){ RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
            using var p = Process.Start(psi)!;
            string o = await p.StandardOutput.ReadToEndAsync(); string e = await p.StandardError.ReadToEndAsync();
            await Task.Run(() => p.WaitForExit());
            return $"UsoClient {cmd}
{o}{e}";
        }
        catch (Exception ex){ return "[ERROR] "+ex.Message; }
    }
}
