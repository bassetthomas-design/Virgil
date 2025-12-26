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
        private readonly Chat.ChatService _chatService;
        private readonly SettingsService _settingsService;
        private readonly MonitoringService _monitoringService;

        public SpecialActionsService(
            Chat.ChatService chatService,
            SettingsService settingsService,
            MonitoringService monitoringService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
        }

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
            return _chatService.ClearHistoryAsync(applyThanosEffect: true, effectDurationMs: 1800);
        }

        /// <summary>
        /// Recharge la configuration de l'application.
        /// Placeholder : l'implémentation réelle dépend de la façon dont la config est stockée.
        /// </summary>
        public Task ReloadSettingsAsync()
        {
            _settingsService.Reload();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Demande un re-scan complet du système de monitoring.
        /// Placeholder : nécessite un lien direct avec MonitoringService.
        /// </summary>
        public Task RescanMonitoringAsync()
        {
            return _monitoringService.RescanAsync();
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
