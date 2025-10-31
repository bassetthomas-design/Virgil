using System.Windows;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Arrêter la surveillance";
            StartMonitoring();
            AddChat("Surveillance activée.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
            StopMonitoring();
            AddChat("Surveillance arrêtée.");
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            AddChat("Mode maintenance activé…");
            // TODO: enchaîner nettoyage total + navigateurs + updates (log + retours)
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            AddChat("Nettoyage intelligent en cours…");
            // TODO: logique de nettoyage intelligent
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            AddChat("Nettoyage navigateurs…");
            // TODO
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            AddChat("Mises à jour totales (apps/jeux/pilotes/Windows/Defender)…");
            // TODO
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            AddChat("Microsoft Defender (MAJ + Scan rapide)…");
            // TODO
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            AddChat("Ouverture de la configuration…");
            // TODO: Show SettingsWindow
        }

        private void AddChat(string text)
        {
            // Ajoute une ligne simple au chat (tu remplaceras par ton template bulles + Thanos)
            UI.ChatItems.Items.Add(text);
            UI.ChatScroll.ScrollToEnd();
        }
    }
}
