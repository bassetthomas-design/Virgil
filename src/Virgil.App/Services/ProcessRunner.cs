using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public static class ProcessRunner
    {
        public static async Task<ProcessResult> RunAsync(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            return new ProcessResult { ExitCode = p.ExitCode, Stdout = await stdoutTask, Stderr = await stderrTask };
        }
    }
}
