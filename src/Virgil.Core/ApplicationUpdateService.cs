using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Met à jour apps/jeux via winget (best-effort). Gère --include-unknown et --silent.
    /// </summary>
    public class ApplicationUpdateService
    {
        public async Task<string> UpgradeAllAsync(bool includeUnknown = true, bool silent = true, bool dryRun = false)
        {
            var args = new StringBuilder("upgrade --all");
            if (includeUnknown) args.Append(" --include-unknown");
            if (silent)         args.Append(" --silent");
            if (dryRun)         args.Append(" --whatif");

            return await RunWingetAsync(args.ToString()).ConfigureAwait(false);
        }

        public async Task<string> ListAsync()
        {
            return await RunWingetAsync("list").ConfigureAwait(false);
        }

        private static async Task<string> RunWingetAsync(string arguments)
        {
            var sb = new StringBuilder();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow = true
                };
                using var p = new Process { StartInfo = psi };
                p.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await p.WaitForExitAsync().ConfigureAwait(false);
                sb.AppendLine($"[winget] ExitCode={p.ExitCode}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[winget] Erreur: {ex.Message}");
            }
            return sb.ToString();
        }
    }
}
