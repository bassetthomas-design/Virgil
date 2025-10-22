using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core
{
    /// <summary>
    /// Pilotage des mises à jour d'apps/jeux via winget.
    /// </summary>
    public sealed class ApplicationUpdateService
    {
        public async Task<string> UpgradeAllAsync(bool includeUnknown = true, bool silent = true)
        {
            var sb = new StringBuilder();
            string args = "upgrade --all";
            if (includeUnknown) args += " --include-unknown";
            if (silent) args += " --silent";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = new Process { StartInfo = psi };
                p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await Task.Run(() => p.WaitForExit());
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[winget error] {ex.Message}");
            }

            return sb.ToString();
        }
    }
}
