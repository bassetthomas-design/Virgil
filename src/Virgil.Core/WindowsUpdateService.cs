using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core
{
    /// <summary>
    /// Provides methods to trigger Windows Update operations using the UsoClient utility.
    /// This service runs a scan, download and install sequence to install available
    /// operating system updates. Output from the commands is captured and returned.
    /// </summary>
    public class WindowsUpdateService
    {
        public async Task<string> UpdateWindowsAsync()
        {
            var output = new StringBuilder();
            var commands = new[]
            {
                ("UsoClient.exe", "StartScan"),
                ("UsoClient.exe", "StartDownload"),
                ("UsoClient.exe", "StartInstall")
            };
            foreach (var (exe, args) in commands)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
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
                    output.AppendLine($"Error running Windows update command {exe} {args}: {ex.Message}");
                }
            }
            return output.ToString();
        }
    }
}