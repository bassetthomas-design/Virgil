using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Assistant;

public interface IRuntimeProcess : IDisposable
{
    bool HasExited { get; }
    int ExitCode { get; }
    bool EnableRaisingEvents { get; set; }
    event DataReceivedEventHandler? OutputDataReceived;
    event DataReceivedEventHandler? ErrorDataReceived;
    event EventHandler? Exited;
    void BeginOutputReadLine();
    void BeginErrorReadLine();
    void CloseMainWindow();
    Task WaitForExitAsync(CancellationToken ct);
    void Kill(bool entireProcessTree);
}
