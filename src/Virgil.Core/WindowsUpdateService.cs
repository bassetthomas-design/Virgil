using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.Core
{
    public sealed class WindowsUpdateStepResult
    {
        public string Command { get; init; } = "";
        public int ExitCode { get; init; }
        public string Output { get; init; } = "";
        public bool TimedOut { get; init; }
        public override string ToString() => $"[{Command}] Exit={ExitCode} Timeout={TimedOut}\n{Output}";
    }

    public sealed class WindowsUpdateAggregateResult
    {
        public WindowsUpdateStepResult Scan { get; init; } = new();
        public WindowsUpdateStepResult Download { get; init; } = new();
        public WindowsUpdateStepResult Install { get; init; } = new();
        public WindowsUpdateStepResult? Restart { get; init; }
        public string Join() => $"{Scan}\n{Download}\n{Install}" + (Restart is null ? "" : $"\n{Restart}");
    }

    public class WindowsUpdateService
    {
        /// <summary>Chemin absolu vers UsoClient.exe (System32).</summary>
        private static string UsoPath
        {
            get
            {
                try
                {
                    var sys = Environment.SystemDirectory; // ex: C:\Windows\System32
                    var p = Path.Combine(sys, "UsoClient.exe");
                    return File.Exists(p) ? p : "UsoClient.exe";
                }
                catch { return "UsoClient.exe"; }
            }
        }

        public static bool IsElevated
        {
            get
            {
                try
                {
                    using var id = WindowsIdentity.GetCurrent();
                    var pr = new WindowsPrincipal(id);
                    return pr.IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch { return false; }
            }
        }

        /// <summary>Lance UsoClient avec capture asynchrone + timeout.</summary>
        private static async Task<WindowsUpdateStepResult> RunUsoAsync(string args, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            timeout ??= TimeSpan.FromMinutes(5);

            var psi = new ProcessStartInfo
            {
                FileName = UsoPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var sb = new StringBuilder();

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            p.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
            p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
            p.Exited += (_, __) => tcs.TrySetResult(p.ExitCode);

            try
            {
                if (!p.Start())
                    return new WindowsUpdateStepResult { Command = $"UsoClient {args}", ExitCode = -1, Output = "Failed to start process." };

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout.Value);

                int exitCode;
                try
                {
                    exitCode = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    try { if (!p.HasExited) p.Kill(true); } catch { }
                    return new WindowsUpdateStepResult { Command = $"UsoClient {args}", ExitCode = -1, TimedOut = true, Output = sb.ToString() };
                }

                return new WindowsUpdateStepResult { Command = $"UsoClient {args}", ExitCode = exitCode, Output = sb.ToString() };
            }
            catch (Exception ex)
            {
                return new WindowsUpdateStepResult { Command = $"UsoClient {args}", ExitCode = -1, Output = $"Exception: {ex.Message}" };
            }
        }

        // Méthodes unitaires
        public Task<WindowsUpdateStepResult> StartScanAsync(TimeSpan? timeout = null, CancellationToken ct = default)
            => RunUsoAsync("StartScan", timeout, ct);

        public Task<WindowsUpdateStepResult> StartDownloadAsync(TimeSpan? timeout = null, CancellationToken ct = default)
            => RunUsoAsync("StartDownload", timeout, ct);

        public Task<WindowsUpdateStepResult> StartInstallAsync(TimeSpan? timeout = null, CancellationToken ct = default)
            => RunUsoAsync("StartInstall", timeout, ct);

        public Task<WindowsUpdateStepResult> RestartDeviceAsync(TimeSpan? timeout = null, CancellationToken ct = default)
            => RunUsoAsync("RestartDevice", timeout, ct);

        /// <summary>Séquence complète : Scan → Download → Install (optionnel: Restart).</summary>
        public async Task<WindowsUpdateAggregateResult> UpdateWindowsAsync(bool andRestart = false, CancellationToken ct = default)
        {
            var scan = await StartScanAsync(TimeSpan.FromMinutes(3), ct).ConfigureAwait(false);
            var dl   = await StartDownloadAsync(TimeSpan.FromMinutes(20), ct).ConfigureAwait(false);
            var inst = await StartInstallAsync(TimeSpan.FromMinutes(30), ct).ConfigureAwait(false);

            WindowsUpdateStepResult? restart = null;
            if (andRestart)
            {
                // Redémarrage (si des updates l’exigent).
                restart = await RestartDeviceAsync(TimeSpan.FromMinutes(5), ct).ConfigureAwait(false);
            }

            return new WindowsUpdateAggregateResult { Scan = scan, Download = dl, Install = inst, Restart = restart };
        }
    }
}
