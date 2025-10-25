using System;
using System.Threading.Tasks;
using System.Windows;
using Virgil.Core;
using Virgil.Core.Services;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Lance un scénario d'entretien complet en arrière-plan.
        /// L'affichage est mis à jour via Progress*/Say* définis dans MainWindow.xaml.cs.
        /// </summary>
        private async void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            // Indique à l'utilisateur que la maintenance démarre
            ProgressIndeterminate("Maintenance complète en cours…", "vigilant");
            try
            {
                var presets = new MaintenancePresetsService();
                // Exécuter la maintenance complète sur un thread de travail pour ne pas bloquer l'UI
                var log = await Task.Run(() => presets.FullAsync()).Unwrap().ConfigureAwait(true);
                // Afficher le rapport dans le chat et mettre à jour la progression
                Say(log, "proud");
                ProgressDone("Maintenance complète terminée.");
            }
            catch (Exception ex)
            {
                Say($"Erreur lors de la maintenance : {ex.Message}", "alert");
                ProgressReset();
            }
        }

        /// <summary>
        /// Nettoie les dossiers TEMP avec une barre de progression détaillée.
        /// </summary>
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            // Réinitialise la progression et démarre l'analyse
            ProgressReset();
            ProgressIndeterminate("Analyse des dossiers TEMP…", "vigilant");
            try
            {
                await Task.Run(() => CleanTempWithProgressInternal()).ConfigureAwait(true);
                ProgressDone("Nettoyage des fichiers TEMP terminé.");
            }
            catch (Exception ex)
            {
                Say($"Erreur lors du nettoyage TEMP : {ex.Message}", "alert");
                ProgressReset();
            }
        }

        /// <summary>
        /// Nettoie les caches des navigateurs pris en charge.
        /// </summary>
        private async void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate("Nettoyage des navigateurs…", "vigilant");
            try
            {
                var service = new BrowserCleaningService();
                var report = await Task.Run(() => service.AnalyzeAndClean(new BrowserCleaningOptions { Force = false })).ConfigureAwait(true);
                Say(report.ToString(), "proud");
                ProgressDone("Nettoyage des navigateurs terminé.");
            }
            catch (Exception ex)
            {
                Say($"Erreur lors du nettoyage des navigateurs : {ex.Message}", "alert");
                ProgressReset();
            }
        }

        /// <summary>
        /// Exécute les mises à jour d'applications, de pilotes et de Windows.
        /// </summary>
        private async void UpdateAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mise à jour des apps via winget
                ProgressIndeterminate("Mise à jour des applications…", "vigilant");
                var apps = new ApplicationUpdateService();
                var appLog = await apps.UpgradeAllAsync(includeUnknown: true, silent: true).ConfigureAwait(true);
                // Mise à jour des pilotes
                ProgressIndeterminate("Mise à jour des pilotes…", "vigilant");
                var drivers = new DriverUpdateService();
                var driverLog = await drivers.UpgradeDriversAsync().ConfigureAwait(true);
                // Mise à jour Windows via UsoClient
                ProgressIndeterminate("Recherche des mises à jour Windows…", "vigilant");
                var wu = new WindowsUpdateService();
                var scanLog     = await wu.StartScanAsync().ConfigureAwait(true);
                var downloadLog = await wu.StartDownloadAsync().ConfigureAwait(true);
                var installLog  = await wu.StartInstallAsync().ConfigureAwait(true);
                // Afficher l'ensemble du rapport
                var combined = $"{appLog}\n{driverLog}\n{scanLog}\n{downloadLog}\n{installLog}";
                Say(combined, "proud");
                ProgressDone("Mises à jour terminées.");
            }
            catch (Exception ex)
            {
                Say($"Erreur lors de la mise à jour : {ex.Message}", "alert");
                ProgressReset();
            }
        }
    }
}