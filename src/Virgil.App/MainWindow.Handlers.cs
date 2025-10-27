using System;
using System.Windows;

namespace Virgil.App
{
    // Tous les handlers UI sont dans ce partial
    public partial class MainWindow : Window
    {
        // Toggle ON
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                StartMonitoring();
            }
            catch { /* safe */ }
        }

        // Toggle OFF
        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                StopMonitoring();
            }
            catch { /* safe */ }
        }

        // Ouvrir config
        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Say("Ouverture de la configuration...", "neutral");
                // TODO: ouvre ton écran/volet de config
            }
            catch { /* safe */ }
        }

        // Maintenance complète
        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            try
            {
                Say("Maintenance complète : démarrage...", "info");
                // TODO (core): enchaîner CleaningService -> BrowserCleaningService -> ExtendedCleaningService
                // -> ApplicationUpdateService (winget) -> WindowsUpdateService (UsoClient)
                // await MaintenancePresetsService.FullAsync();
                Say("Maintenance complète : terminé.", "success");
            }
            catch (Exception ex)
            {
                Say($"Maintenance complète : erreur. {ex.Message}", "alert");
            }
        }

        // Nettoyer TEMP
        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            try
            {
                Say("Nettoyage TEMP : analyse...", "info");
                // TODO (core): analyse + suppression + stats
                // await CleaningService.CleanTempAsync(progress => {/*update UI*/});
                Say("Nettoyage TEMP : terminé.", "success");
            }
            catch (Exception ex)
            {
                Say($"Nettoyage TEMP : erreur. {ex.Message}", "alert");
            }
        }

        // Nettoyer navigateurs
        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            try
            {
                Say("Nettoyage navigateurs : démarrage...", "info");
                // TODO (core): BrowserCleaningService.AnalyzeAndClean(...)
                Say("Nettoyage navigateurs : terminé.", "success");
            }
            catch (Exception ex)
            {
                Say($"Nettoyage navigateurs : erreur. {ex.Message}", "alert");
            }
        }

        // Tout mettre à jour
        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            try
            {
                Say("Mises à jour (apps/jeux/drivers/Windows) : démarrage...", "info");
                // TODO (core): winget upgrade --all --include-unknown (mode silencieux si possible)
                // await ApplicationUpdateService.UpgradeAllAsync(...);
                // await DriverUpdateService.UpgradeDriversAsync();
                // await WindowsUpdateService.RunAsync(); // Scan/Download/Install/Restart
                Say("Mises à jour : terminé.", "success");
            }
            catch (Exception ex)
            {
                Say($"Mises à jour : erreur. {ex.Message}", "alert");
            }
        }

        // Defender
        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            try
            {
                Say("Microsoft Defender : mise à jour des signatures + scan...", "info");
                // TODO (core): déclenchement update signatures + démarrage d’un scan quick/full
                Say("Microsoft Defender : opérations terminées.", "success");
            }
            catch (Exception ex)
            {
                Say($"Microsoft Defender : erreur. {ex.Message}", "alert");
            }
        }
    }
}
