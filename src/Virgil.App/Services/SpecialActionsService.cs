using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Virgil.App.Services
{
    /// <summary>
    /// Actions spéciales / utilitaires à plus haut niveau d'impact.
    /// Implémentations volontairement prudentes pour l'instant.
    /// </summary>
    public class SpecialActionsService : ISpecialActionsService
    {
        /// <summary>
        /// Mode "RAMBO" : tentative de réparation rapide de l'environnement shell.
        /// Ici, on redémarre simplement explorer.exe.
        /// </summary>
        public Task RamboRepairAsync()
        {
            const string cmd = "taskkill /F /IM explorer.exe & start explorer.exe";
            return StartProcessAsync("cmd.exe", $"/c {cmd}");
        }

        /// <summary>
        /// Effet Thanos sur l'historique de chat.
        /// Pour l'instant, ce backend ne fait rien car la persistance réelle du chat
        /// n'est pas gérée à ce niveau. L'effacement visuel doit être géré côté UI/service de chat.
        /// </summary>
        public Task PurgeChatHistoryAsync()
        {
            // TODO: brancher ici un service de persistance de l'historique (si existant).
            return Task.CompletedTask;
        }

        /// <summary>
        /// Recharge la configuration de l'application.
        /// Placeholder : l'implémentation réelle dépend de la façon dont la config est stockée.
        /// </summary>
        public Task ReloadSettingsAsync()
        {
            // TODO: injecter et appeler un service de configuration si/ quand il sera disponible.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Demande un re-scan complet du système de monitoring.
        /// Placeholder : nécessite un lien direct avec MonitoringService.
        /// </summary>
        public Task RescanMonitoringAsync()
        {
            // TODO: brancher ici MonitoringService.RescanAsync() quand il sera exposé.
            return Task.CompletedTask;
        }

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
                    tcs.TrySetException(new InvalidOperationException("Impossible de démarrer le processus spécial."));
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
