using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core
{
    /// <summary>
    /// Provides methods to update system drivers. The current implementation
    /// uses winget where possible and returns a message indicating that
    /// driver updates are limited.
    /// </summary>
    public class DriverUpdateService
    {
        public async Task<string> UpdateDriversAsync()
        {
            var output = new StringBuilder();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "upgrade --all --include-unknown --silent",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                output.AppendLine($"Error running driver updates: {ex.Message}");
            }

            output.AppendLine("Driver update support is experimental and may not cover all hardware. Use manufacturer tools for complete updates.");
            return output.ToString();
        }
    }
}