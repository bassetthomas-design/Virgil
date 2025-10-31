using System.Windows;
using System.Windows.Controls;

namespace Virgil.App
{
    // IMPORTANT : ne pas implémenter IComponentConnector ici
    // Ne pas déclarer InitializeComponent, _contentLoaded, Connect, _CreateDelegate

    public partial class MainWindow
    {
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Surveillance activée";
            StartMonitoring(); // si présent dans MainWindow.Monitoring.cs
            AddChat("👁️ Surveillance activée.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
            StopMonitoring(); // si présent dans MainWindow.Monitoring.cs
            AddChat("😴 Surveillance arrêtée.");
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            AddChat("🔧 Maintenance complète lancée.");
            // TODO: appeler le workflow réel
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            AddChat("🧹 Nettoyage intelligent lancé.");
            // TODO
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            AddChat("🌐 Nettoyage navigateurs lancé.");
            // TODO
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            AddChat("⬆️ Mises à jour totales lancées.");
            // TODO
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            AddChat("🛡️ Microsoft Defender (MAJ + Scan) lancé.");
            // TODO
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        // --- utilitaire chat ---
        private void AddChat(string message)
        {
            if (ChatItems == null || ChatScroll == null) return;
            var tb = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
            ChatItems.Items.Add(tb);
            ChatScroll.ScrollToEnd();
        }
    }
}
