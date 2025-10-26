using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Virgil.Core.Services
{
    /// <summary>
    /// Windows Defender operations via MpCmdRun.exe (signature update + scans).
    /// </summary>
    public sealed class DefenderUpdateService
    {
        private static string GetMpCmdPath()
        {
            string[] candidates = new[]
            {
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Windows Defender\MpCmdRun.exe"),
                @"C:\ProgramData\Microsoft\Windows Defender\Platform\4.0.0.0\MpCmdRun.exe"
            };
            foreach (var c in candidates)
            {
                if (System.IO.File.Exists(c)) return c;
            }
            return "MpCmdRun.exe"; // PATH fallback
        }

        private static async Task<string> RunMpAsync(string args)
        {
            var exe  = GetMpCmdPath();
            var psi  = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            var sb = new StringBuilder();
            using (var p = new Process { StartInfo = psi })
            {
                p.Start();
                sb.AppendLine(await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false));
                sb.AppendLine(await p.StandardError.ReadToEndAsync().ConfigureAwait(false));
                p.WaitForExit();
                sb.AppendLine($"ExitCode={p.ExitCode}");
            }
            return sb.ToString();
        }

        public Task<string> UpdateSignaturesAsync() => RunMpAsync("-SignatureUpdate");
        public Task<string> QuickScanAsync()        => RunMpAsync("-Scan -ScanType 1");
        public Task<string> FullScanAsync()         => RunMpAsync("-Scan -ScanType 2");
    }
}
