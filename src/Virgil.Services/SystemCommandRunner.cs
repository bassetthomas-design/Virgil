using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services;

public interface ISystemCommandRunner
{
    Task<CommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default);
}

public sealed record CommandResult(bool Success, string? Output = null, string? Error = null)
{
    public string? PickMessage() => !string.IsNullOrWhiteSpace(Output) ? Output : Error;
}

public sealed class SystemCommandRunner : ISystemCommandRunner
{
    public async Task<CommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

            if (!process.Start())
            {
                return new CommandResult(false, Error: "Impossible de démarrer le processus système");
            }

            var finished = await Task.WhenAny(tcs.Task, Task.Delay(timeout, ct)).ConfigureAwait(false);
            if (finished != tcs.Task)
            {
                TryKill(process);
                return new CommandResult(false, Error: "Commande expirée");
            }

            var exitCode = await tcs.Task.ConfigureAwait(false);
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

            return new CommandResult(exitCode == 0, string.IsNullOrWhiteSpace(output) ? null : output.Trim(), string.IsNullOrWhiteSpace(error) ? null : error.Trim());
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
        {
            return new CommandResult(false, Error: ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}
