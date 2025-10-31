using System.Windows;

namespace Virgil.App
{
    // Un simple partial qui contient UNIQUEMENT des handlers et helpers
    public partial class MainWindow
    {
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            try { Resources["SurveillanceToggleText"] = "Arrêter la surveillance"; } catch { }
            // TODO: démarrer la surveillance (timers, capteurs, etc.)
            AddChat("Surveillance activée.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try { Resources["SurveillanceToggleText"] = "Démarrer la surveillance"; } catch { }
            // TODO: arrêter la surveillance
            AddChat("Surveillance arrêtée.");
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            // TODO: enchaîner Nettoyage intelligent + Navigateurs + Mises à jour
            AddChat("Maintenance complète : démarrage…");
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            // TODO: nettoyage intelligent seul
            AddChat("Nettoyage intelligent lancé.");
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            // TODO: nettoyage des navigateurs
            AddChat("Nettoyage navigateurs en cours.");
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            // TODO: winget + pilotes + Windows Update + Defender
            AddChat("Mises à jour complètes démarrées.");
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            // TODO: MAJ Defender + scan rapide
            AddChat("Microsoft Defender : mise à jour + scan.");
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            // TODO: ouvrir la fenêtre de configuration
            AddChat("Ouverture de la configuration…");
            try
            {
                var win = new SettingsWindow();
                win.Owner = this;
                win.ShowDialog();
            }
            catch { /* ignore si SettingsWindow pas encore prêt */ }
        }

        // Affichage dans la zone de chat (ItemsControl x:Name="ChatItems")
        private void AddChat(string message)
        {
            try
            {
                ChatItems?.Items?.Add(message);
                ChatScroll?.ScrollToEnd();
            }
            catch { /* en cas d’absence de l’UI */ }
        }
    }
}
