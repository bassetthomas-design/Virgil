using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Virgil.App
{
    public partial class MainWindow
    {
        // === Handlers du Toggle Surveillance ===
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Arrêter la surveillance";
            PostChat("Surveillance activée.");
            // TODO: démarrer le monitoring (timers, sondes, etc.)
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
            PostChat("Surveillance arrêtée.");
            // TODO: arrêter le monitoring
        }

        // === Handlers des actions bas de page ===
        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            PostChat("Maintenance complète : démarrage…");
            await Task.Run(() => { /* TODO: enchaîner nettoyage + navigateurs + updates */ });
            PostChat("Maintenance complète terminée.");
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            PostChat("Nettoyage intelligent : démarrage…");
            await Task.Run(() => { /* TODO: Clean smart */ });
            PostChat("Nettoyage intelligent terminé.");
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            PostChat("Nettoyage navigateurs : démarrage…");
            await Task.Run(() => { /* TODO: Clean browsers */ });
            PostChat("Navigateurs nettoyés.");
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            PostChat("Mises à jour globales : démarrage…");
            await Task.Run(() => { /* TODO: winget + pilotes + Windows Update + Defender */ });
            PostChat("Mises à jour complètes terminées.");
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            PostChat("Microsoft Defender : MAJ + Scan rapide…");
            await Task.Run(() => { /* TODO: Defender update + quick scan */ });
            PostChat("Defender : opération terminée.");
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var w = new SettingsWindow
            {
                Owner = this
            };
            w.ShowDialog();
        }

        // === Utilitaire d’affichage chat (existant côté XAML : ItemsControl x:Name=\"ChatItems\") ===
        private void PostChat(string text)
        {
            ChatItems.Items.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
            ChatScroll.ScrollToEnd();
        }
    }
}
