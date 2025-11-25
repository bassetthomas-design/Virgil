using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    /// <summary>
    /// Implémentation de base pour les actions liées aux performances.
    /// Certaines actions utilisent des commandes système classiques (powercfg, Task Manager).
    /// </summary>
    public class PerformanceActionsService : IPerformanceActionsService
    {
        /// <summary>
        /// Active un profil de performance élevé (si disponible) via powercfg.
        /// </summary>
        public Task EnablePerfModeAsync()
        {
            // Utilisation du GUID du plan "Hautes performances" par défaut de Windows.
            const string cmd = "powercfg /S 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
            return StartProcessAsync("cmd.exe", $"/c {cmd}");
        }

        /// <summary>
        /// Revient au profil d'alimentation équilibré.
        /// </summary>
        public Task DisablePerfModeAsync()
        {
            // GUID du plan "Utilisation normale" (équilibré).
            const string cmd = "powercfg /S 381b4222-f694-41f0-9685-ff5bb260df2e";
            return StartProcessAsync("cmd.exe", $"/c {cmd}");
        }

        /// <summary>
        /// Ouvre le Gestionnaire des tâches pour analyse manuelle du démarrage.
        /// </summary>
        public Task AnalyzeStartupAsync()
        {
            // Ouvre le Gestionnaire des tâches (l'utilisateur peut ensuite aller dans l'onglet Démarrage).
            return StartProcessAsync("taskmgr.exe", string.Empty);
        }

        /// <summary>
        /// Action placeholder pour "couper les apps de fond (gaming)".
        /// À implémenter plus finement selon la politique de l'application.
        /// </summary>
        public Task KillGamingSessionProcessesAsync()
        {
            // Par sécurité, on ne tue encore aucun process automatiquement ici.
            // Cette méthode pourra cibler certains processus (launchers, overlays, etc.)
            // une fois la liste définie.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Helper générique pour lancer un process et attendre sa fin.
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
                    tcs.TrySetException(new InvalidOperationException("Impossible de démarrer le processus de performance."));
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
