using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public sealed class RuntimeProcessRunner : IRuntimeProcessRunner
{
    public IRuntimeProcess? Start(ProcessStartInfo startInfo)
    {
        var process = Process.Start(startInfo);
        return process is null ? null : new RuntimeProcessAdapter(process);
    }

    private sealed class RuntimeProcessAdapter : IRuntimeProcess
    {
        private readonly Process _process;

        public RuntimeProcessAdapter(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public bool HasExited => _process.HasExited;

        public int ExitCode => _process.ExitCode;

        public bool EnableRaisingEvents
        {
            get => _process.EnableRaisingEvents;
            set => _process.EnableRaisingEvents = value;
        }

        public event DataReceivedEventHandler? OutputDataReceived
        {
            add => _process.OutputDataReceived += value;
            remove => _process.OutputDataReceived -= value;
        }

        public event DataReceivedEventHandler? ErrorDataReceived
        {
            add => _process.ErrorDataReceived += value;
            remove => _process.ErrorDataReceived -= value;
        }

        public event EventHandler? Exited
        {
            add => _process.Exited += value;
            remove => _process.Exited -= value;
        }

        public void BeginOutputReadLine() => _process.BeginOutputReadLine();

        public void BeginErrorReadLine() => _process.BeginErrorReadLine();

        public void CloseMainWindow() => _process.CloseMainWindow();

        public Task WaitForExitAsync(CancellationToken ct) => _process.WaitForExitAsync(ct);

        public void Kill(bool entireProcessTree) => _process.Kill(entireProcessTree);

        public void Dispose() => _process.Dispose();
    }
}
