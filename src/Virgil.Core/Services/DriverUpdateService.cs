using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.Core.Services;

public sealed class DriverUpdateService
{
    public Task<string> UpgradeDriversAsync()
        => RunProcessAsync("winget", "upgrade --all --include-unknown --silent");

    private static async Task<string> RunProcessAsync(string file, string args)
    {
        try{
            var psi = new ProcessStartInfo(file, args){ RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            string stdout = await p.StandardOutput.ReadToEndAsync(); string stderr = await p.StandardError.ReadToEndAsync();
            await Task.Run(() => p.WaitForExit());
            return $"$ {file} {args}
--- STDOUT ---
{stdout}
--- STDERR ---
{stderr}";
        }catch(Exception ex){ return "[ERROR] "+ex.Message; }
    }
}
