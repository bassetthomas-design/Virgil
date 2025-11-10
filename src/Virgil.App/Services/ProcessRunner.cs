using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    public static class ProcessRunner
    {
        public static async Task<int> RunAsync(string fileName, string args)
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
            await p.WaitForExitAsync();
            return p.ExitCode;
        }
    }
}
