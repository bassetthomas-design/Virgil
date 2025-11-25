using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    /// <summary>
    /// Implémentation des actions réseau rapides déclenchées depuis le panneau d'actions.
    /// Pour l'instant, les actions utilisent des commandes système classiques (cmd / netsh / ipconfig).
    /// </summary>
    public class NetworkActionsService : INetworkActionsService
    {
        /// <summary>
        /// Lance un diagnostic réseau Windows.
        /// </summary>
        public Task RunDiagnosticsAsync()
        {
            // Utilise l'outil de diagnostic réseau de Windows (peut ouvrir une UI).
            return StartProcessAsync("cmd.exe", "/c msdt.exe /id NetworkDiagnosticsNetworkAdapter");
        }

        /// <summary>
        /// Réinitialisation légère du réseau : flush DNS et cache NetBIOS.
        /// </summary>
        public Task SoftResetAsync()
        {
            const string cmd = "ipconfig /flushdns && nbtstat -R && nbtstat -RR";
            return StartProcessAsync("cmd.exe", $"/c {cmd}");
        }

        /// <summary>
        /// Réinitialisation plus profonde : reset TCP/IP + Winsock.
        /// Peut nécessiter un redémarrage pour être pleinement appliqué.
        /// </summary>
        public Task HardResetAsync()
        {
            const string cmd = "netsh int ip reset && netsh winsock reset";
            return StartProcessAsync("cmd.exe", $"/c {cmd}");
        }

        /// <summary>
        /// Test de latence simple via ping sur un endpoint public.
        /// </summary>
        public Task RunLatencyTestAsync()
        {
            // Ici on ping 1.1.1.1 (Cloudflare). À adapter si besoin.
            const string cmd = "ping 1.1.1.1 -n 10";
            return StartProcessAsync("cmd.exe", $"/c {cmd}");
        }

        /// <summary>
        /// Helper générique pour lancer un process en arrière-plan et attendre sa fin.
        /// </summary>
        private static Task StartProcessAsync(string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<object?>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false
                };

                var process = new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                process.Exited += (_, _) =>
                {
                    try
                    {
                        process.Dispose();
                    }
                    catch
                    {
                        // Ignorer les erreurs de dispose.
                    }

                    tcs.TrySetResult(null);
                };

                if (!process.Start())
                {
                    tcs.TrySetException(new InvalidOperationException("Impossible de démarrer le processus réseau."));
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }
    }
}
