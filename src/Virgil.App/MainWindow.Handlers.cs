using System.Windows;
using System.Windows.Controls;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Surveillance activée";
            // TODO: démarrer la surveillance réelle ici
            AddChat("👁️ Surveillance activée.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
            // TODO: arrêter la surveillance réelle ici
            AddChat("😴 Surveillance arrêtée.");
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            AddChat("🔧 Maintenance complète lancée (placeholder).");
            // TODO: appeler le workflow réel
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            AddChat("🧹 Nettoyage intelligent lancé (placeholder).");
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            AddChat("🌐 Nettoyage navigateurs lancé (placeholder).");
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            AddChat("⬆️ Mises à jour totales lancées (placeholder).");
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            AddChat("🛡️ Microsoft Defender (MAJ + Scan) lancé (placeholder).");
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow();
            win.Owner = this;
            win.ShowDialog();
        }

        // Utilitaire chat
        private void AddChat(string message)
        {
            var tb = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap };
            ChatItems.Items.Add(tb);
            ChatScroll.ScrollToEnd();
        }
    }
}
