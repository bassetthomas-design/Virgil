using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Virgil.Core.Services;

public sealed class DefenderUpdateService
{
    public async Task<string> UpdateSignaturesAsync() => await RunMpCmdAsync("-SignatureUpdate");
    public async Task<string> QuickScanAsync()       => await RunMpCmdAsync("-Scan -ScanType 1");

    private static async Task<string> RunMpCmdAsync(string args)
    {
        string? mpcmd = TryPaths(new[]{
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender\MpCmdRun.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender\MpCmdRun\MpCmdRun.exe")
        });
        if (string.IsNullOrEmpty(mpcmd)) return "[ERROR] MpCmdRun.exe introuvable";
        try
        {
            var psi = new ProcessStartInfo(mpcmd, args){ RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
            using var p = Process.Start(psi)!; string o = await p.StandardOutput.ReadToEndAsync(); string e = await p.StandardError.ReadToEndAsync();
            await Task.Run(() => p.WaitForExit());
            return $"{mpcmd} {args}
{o}{e}";
        }
        catch (Exception ex){ return "[ERROR] "+ex.Message; }
    }

    private static string? TryPaths(string[] candidates){ foreach(var c in candidates) if(File.Exists(c)) return c; return null; }
}
