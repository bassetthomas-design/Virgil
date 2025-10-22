using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core
{
    public sealed class WindowsUpdateAggregateResult
    {
        public string? Scan { get; set; }
        public string? Download { get; set; }
        public string? Install { get; set; }
        public string? Restart { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Scan)) sb.AppendLine("[Scan]").AppendLine(Scan);
            if (!string.IsNullOrWhiteSpace(Download)) sb.AppendLine("[Download]").AppendLine(Download);
            if (!string.IsNullOrWhiteSpace(Install)) sb.AppendLine("[Install]").AppendLine(Install);
            if (!string.IsNullOrWhiteSpace(Restart)) sb.AppendLine("[Restart]").AppendLine(Restart);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Contrôle basique de Windows Update via UsoClient.exe
    /// </summary>
    public sealed class WindowsUpdateService
    {
        public async Task<string> StartScanAsync()      => await RunUsoAsync("StartScan");
        public async Task<string> StartDownloadAsync()  => await RunUsoAsync("StartDownload");
        public async Task<string> StartInstallAsync()   => await RunUsoAsync("StartInstall");
        public async Task<string> RestartDeviceAsync()  => await RunUsoAsync("RestartDevice");

        public async Task<WindowsUpdateAggregateResult> UpdateWindowsAsync(bool restartAfter = false)
        {
            var agg = new WindowsUpdateAggregateResult
            {
                Scan = await StartScanAsync(),
                Download = await StartDownloadAsync(),
                Install = await StartInstallAsync(),
                Restart = restartAfter ? await RestartDeviceAsync() : null
            };
            return agg;
        }

        private static async Task<string> RunUsoAsync(string arg)
        {
            string uso = Path.Combine(Environment.SystemDirectory, "UsoClient.exe");
            if (!File.Exists(uso)) uso = "UsoClient.exe";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uso,
                    Arguments = arg,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = new Process { StartInfo = psi };
                var sb = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await Task.Run(() => p.WaitForExit());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[UsoClient error] {ex.Message}";
            }
        }
    }
}
