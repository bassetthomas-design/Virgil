using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Services.Network;

public sealed class NetworkCommandRunner : INetworkCommandRunner
{
    public async Task<NetworkCommandResult> RunAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken ct = default)
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
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

            if (!process.Start())
            {
                return new NetworkCommandResult(false, Error: "Impossible de démarrer le processus");
            }

            var waitTask = tcs.Task;
            var timeoutTask = Task.Delay(timeout, ct);
            var finished = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);

            if (finished == timeoutTask)
            {
                TryKill(process);
                return new NetworkCommandResult(false, Error: "Temps d'exécution dépassé");
            }

            var exitCode = await waitTask.ConfigureAwait(false);
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

            return new NetworkCommandResult(exitCode == 0, string.IsNullOrWhiteSpace(output) ? null : output.Trim(), string.IsNullOrWhiteSpace(error) ? null : error.Trim());
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
        {
            return new NetworkCommandResult(false, Error: ex.Message);
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
            // Best effort: ignorer les échecs de kill.
        }
    }
}
