using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.App.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(string fileName, string arguments, Action<string>? onOutput = null, Action<string>? onError = null, bool elevate = false)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (elevate)
        {
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.RedirectStandardOutput = false;
            psi.RedirectStandardError = false;
        }

        using var p = new Process { StartInfo = psi };
        if (onOutput != null) p.OutputDataReceived += (_, e) => { if (e.Data!=null) onOutput(e.Data); };
        if (onError != null)  p.ErrorDataReceived  += (_, e) => { if (e.Data!=null) onError(e.Data);  };
        p.Start();
        if (!elevate) { p.BeginOutputReadLine(); p.BeginErrorReadLine(); }
        await p.WaitForExitAsync();
        return p.ExitCode;
    }
}
