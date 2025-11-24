using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.ViewModels
{
    public partial class DashboardViewModel
    {
        /// <summary>
        /// Helper pour encapsuler narration + exécution d'une action avec reporting de succès/échec.
        /// </summary>
        private async Task RunWithNarrationAsync(string actionId, Func<Task> action)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException("actionId must not be null or empty.", nameof(actionId));
            if (action is null)
                throw new ArgumentNullException(nameof(action));

            var success = false;

            try
            {
                await _virgil.OnActionStartedAsync(actionId, CancellationToken.None);
                await action();
                success = true;
            }
            catch (Exception ex)
            {
                AppendChat($"❌ Une erreur est survenue pendant l’action « {actionId} » : {ex.Message}");
            }
            finally
            {
                await _virgil.OnActionCompletedAsync(actionId, success, CancellationToken.None);
            }
        }

        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            AppendChat(IsSurveillanceEnabled
                ? "Surveillance activée."
                : "Surveillance arrêtée.");
        }

        public void RunMaintenance()
        {
            _ = RunMaintenanceAsync();
        }

        private async Task RunMaintenanceAsync()
        {
            await RunWithNarrationAsync("maintenance_full",
                async () =>
                {
                    AppendChat("Maintenance complète : démarrage.");
                    await _systemActions.RunMaintenanceAsync();
                    AppendChat("Maintenance complète : terminée.");
                });
        }

        public void CleanTempFiles()
        {
            _ = CleanTempFilesAsync();
        }

        private async Task CleanTempFilesAsync()
        {
            await RunWithNarrationAsync("smart_cleanup",
                async () =>
                {
                    AppendChat("Nettoyage intelligent des fichiers temporaires : démarrage.");
                    await _systemActions.RunSmartCleanupAsync();
                    AppendChat("Nettoyage intelligent : terminé.");
                });
        }

        public void CleanBrowsers()
        {
            _ = CleanBrowsersAsync();
        }

        private async Task CleanBrowsersAsync()
        {
            await RunWithNarrationAsync("browsers_clean",
                async () =>
                {
                    AppendChat("Nettoyage des navigateurs : démarrage.");
                    await _systemActions.RunCleanBrowsersAsync();
                    AppendChat("Nettoyage des navigateurs : terminé.");
                });
        }

        public void UpdateAll()
        {
            _ = UpdateAllAsync();
        }

        private async Task UpdateAllAsync()
        {
            await RunWithNarrationAsync("updates_all",
                async () =>
                {
                    AppendChat("Mise à jour globale du système et des applications : démarrage.");
                    await _systemActions.RunUpdateAllAsync();
                    AppendChat("Mises à jour : terminées.");
                });
        }

        public void RunDefenderScan()
        {
            _ = RunDefenderScanAsync();
        }

        private async Task RunDefenderScanAsync()
        {
            await RunWithNarrationAsync("defender_scan",
                async () =>
                {
                    AppendChat("Analyse de sécurité Windows Defender : démarrage.");
                    await _systemActions.RunDefenderAsync();
                    AppendChat("Analyse de sécurité : terminée.");
                });
        }

        public void OpenConfiguration()
        {
            _ = OpenConfigurationAsync();
        }

        private async Task OpenConfigurationAsync()
        {
            await RunWithNarrationAsync("open_config",
                async () =>
                {
                    AppendChat("Ouverture du centre de configuration de Virgil.");
                    await _systemActions.OpenConfigAsync();
                });
        }
    }
}
