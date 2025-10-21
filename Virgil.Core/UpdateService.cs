using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core
{
    /// <summary>
    /// Provides methods to check for and apply application and system
    /// updates via the Windows Package Manager (winget). This service
    /// encapsulates the process invocation and captures output for
    /// display in the UI.
    /// </summary>
    public class UpdateService
    {
        /// <summary>
        /// Executes <c>winget upgrade --all --silent</c> to upgrade all
        /// installed packages. The method returns the collected output
        /// (stdout and stderr) when the process completes. If winget
        /// is not available, the returned string will contain the
        /// corresponding error message.
        /// </summary>
        /// <returns>A task that resolves to the captured output.</returns>
        public async Task<string> UpgradeAllAsync()
        {
            var output = new StringBuilder();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "upgrade --all --silent",
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
                output.AppendLine($"Error running winget: {ex.Message}");
            }

            return output.ToString();
        }
    }
}