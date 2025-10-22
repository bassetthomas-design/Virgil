using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Enchaîne les commandes UsoClient (scan / download / install / restart).
    /// Affiche des messages propres si droits insuffisants.
    /// </summary>
    public class WindowsUpdateService
    {
        public async Task<string> StartScanAsync()     => await RunUsoAsync("StartScan");
        public async Task<string> StartDownloadAsync() => await RunUsoAsync("StartDownload");
        public async Task<string> StartInstallAsync()  => await RunUsoAsync("StartInstall");
        public async Task<string> RestartDeviceAsync() => await RunUsoAsync("RestartDevice");

        private static async Task<string> RunUsoAsync(string verb)
        {
            var sb = new StringBuilder();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "UsoClient.exe",
                    Arguments = verb,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow = true,
                    Verb = "runas" // si UAC possible, sinon ignoré
                };
                using var p = new Process { StartInfo = psi };
                p.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                try
                {
                    p.Start();
                }
                catch (System.ComponentModel.Win32Exception w32) // UAC refusé / non admin
                {
                    sb.AppendLine($"[WU] Droit admin requis ou refusé: {w32.Message}");
                    return sb.ToString();
                }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await p.WaitForExitAsync().ConfigureAwait(false);
                sb.AppendLine($"[WU] {verb} - ExitCode={p.ExitCode}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[WU] Erreur {verb}: {ex.Message}");
            }
            return sb.ToString();
        }
    }
}
