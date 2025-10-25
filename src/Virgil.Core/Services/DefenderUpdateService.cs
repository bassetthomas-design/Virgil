using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Provides methods to update Windows Defender signatures and perform scans.
    /// Uses MpCmdRun.exe to execute update and scan commands.
    /// </summary>
    public sealed class DefenderUpdateService
    {
        /// <summary>
        /// Updates Windows Defender virus definitions (signature update).
        /// </summary>
        /// <returns>Aggregated console output.</returns>
        public async Task<string> UpdateSignaturesAsync()
        {
            return await RunMpCmdAsync("-SignatureUpdate").ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a quick scan using Windows Defender.
        /// </summary>
        /// <returns>Aggregated console output.</returns>
        public async Task<string> QuickScanAsync()
        {
            return await RunMpCmdAsync("-Scan -ScanType 1").ConfigureAwait(false);
        }

        /// <summary>
        /// Performs a full scan using Windows Defender.
        /// </summary>
        /// <returns>Aggregated console output.</returns>
        public async Task<string> FullScanAsync()
        {
            return await RunMpCmdAsync("-Scan -ScanType 2").ConfigureAwait(false);
        }

        private static async Task<string> RunMpCmdAsync(string args)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "MpCmdRun.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    var sb = new StringBuilder();
                    process.Start();
                    sb.AppendLine(await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false));
                    sb.AppendLine(await process.StandardError.ReadToEndAsync().ConfigureAwait(false));
                    await process.WaitForExitAsync().ConfigureAwait(false);
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                return $"[DEFENDER] Error: {ex.Message}";
            }
        }
    }
}
