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
    public sealed record WindowsUpdateCommandResult(string Verb, int? ExitCode, string Output)
    {
        public string GetDisplayMessage()
        {
            if (!string.IsNullOrWhiteSpace(Output))
            {
                return Output.Trim();
            }

            return $"[WU] {Verb} terminé.";
        }
    }

    public class WindowsUpdateService
    {
        public async Task<WindowsUpdateCommandResult> StartScanAsync()     => await RunUsoAsync("StartScan");
        public async Task<WindowsUpdateCommandResult> StartDownloadAsync() => await RunUsoAsync("StartDownload");
        public async Task<WindowsUpdateCommandResult> StartInstallAsync()  => await RunUsoAsync("StartInstall");
        public async Task<WindowsUpdateCommandResult> RestartDeviceAsync() => await RunUsoAsync("RestartDevice");

        private static async Task<WindowsUpdateCommandResult> RunUsoAsync(string verb)
        {
            var sb = new StringBuilder();
            int? exitCode = null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "UsoClient.exe",
                    Arguments = verb,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // si UAC possible, sinon ignoré
                };
                using var p = new Process { StartInfo = psi };
                p.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                try
                {
                    p.Start();
                }
                catch (System.ComponentModel.Win32Exception w32) // UAC refusé / non admin
                {
                    sb.AppendLine($"[WU] Droit admin requis ou refusé: {w32.Message}");
                    return new WindowsUpdateCommandResult(verb, null, sb.ToString());
                }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await p.WaitForExitAsync().ConfigureAwait(false);
                exitCode = p.ExitCode;
                Debug.WriteLine($"[WU] {verb} ExitCode={p.ExitCode}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[WU] Erreur {verb}: {ex.Message}");
            }

            return new WindowsUpdateCommandResult(verb, exitCode, sb.ToString());
        }
    }
}
