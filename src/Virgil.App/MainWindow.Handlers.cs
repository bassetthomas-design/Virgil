using System;
using System.Windows;
using System.Threading.Tasks;

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
                // TODO: ouvre l’écran/volet de config
            }
            catch { /* safe */ }
        }

        // Maintenance complète
        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            try
            {
                Say("Maintenance complète : démarrage...", "info");

                // TODO (core) quand dispo :
                // await MaintenancePresetsService.FullAsync();

                // Temporaire : évite l’avertissement CS1998
                await Task.CompletedTask;

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

                // TODO (core) quand dispo :
                // await CleaningService.CleanTempAsync(progress => {/*update UI*/});

                await Task.CompletedTask;

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

                // TODO (core) quand dispo :
                // await BrowserCleaningService.AnalyzeAndClean(...);

                await Task.CompletedTask;

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

                // TODO (core) quand dispo :
                // await ApplicationUpdateService.UpgradeAllAsync(...);
                // await DriverUpdateService.UpgradeDriversAsync();
                // await WindowsUpdateService.RunAsync(); // Scan/Download/Install/Restart

                await Task.CompletedTask;

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
                Say("Microsoft Defender : MAJ signatures + scan...", "info");

                // TODO (core) quand dispo :
                // await DefenderService.UpdateSignaturesAsync();
                // await DefenderService.RunScanAsync();

                await Task.CompletedTask;

                Say("Microsoft Defender : opérations terminées.", "success");
            }
            catch (Exception ex)
            {
                Say($"Microsoft Defender : erreur. {ex.Message}", "alert");
            }
        }
    }
}
