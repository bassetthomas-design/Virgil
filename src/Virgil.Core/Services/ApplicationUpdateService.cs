using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services;

public sealed class ApplicationUpdateService
{
    public async Task<string> UpgradeAllAsync(bool includeUnknown = true, bool silent = true)
    {
        var args = new StringBuilder("upgrade --all");
        if (includeUnknown) args.Append(" --include-unknown");
        if (silent) args.Append(" --silent");
        return await RunProcessAsync("winget", args.ToString());
    }

    private static async Task<string> RunProcessAsync(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args){ RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi)!;
            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await Task.Run(() => p.WaitForExit());
            return $"$ {file} {args}
--- STDOUT ---
{stdout}
--- STDERR ---
{stderr}";
        }
        catch (Exception ex) { return "[ERROR] " + ex.Message; }
    }
}
