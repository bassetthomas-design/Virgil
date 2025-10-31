using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        public MainWindow()
        {
            InitializeComponent();

            // Horloge UI
            _clockTimer.Tick += (_, __) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            // Texte initial du toggle
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
        }

        // ========= Handlers UI (référencés dans MainWindow.UI.xaml) =========

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Arrêter la surveillance";
            // TODO: démarrer la surveillance ici
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
            // TODO: arrêter la surveillance ici
        }

        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            // TODO: appelle ton service de maintenance complète
            await System.Threading.Tasks.Task.CompletedTask;
            AppendChat("Maintenance complète lancée → terminé.");
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            // TODO: nettoyage intelligent
            await System.Threading.Tasks.Task.CompletedTask;
            AppendChat("Nettoyage intelligent terminé.");
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            // TODO: nettoyage navigateurs
            await System.Threading.Tasks.Task.CompletedTask;
            AppendChat("Navigateurs nettoyés.");
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            // TODO: mises à jour (apps / pilotes / Windows)
            await System.Threading.Tasks.Task.CompletedTask;
            AppendChat("Mises à jour effectuées.");
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            // TODO: MAJ + Scan Defender
            await System.Threading.Tasks.Task.CompletedTask;
            AppendChat("Microsoft Defender : mise à jour + scan terminés.");
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        // ========= Helpers =========

        private void AppendChat(string text)
        {
            var tb = new TextBlock { Text = $"• {text}", Margin = new Thickness(4, 2, 4, 2) };
            ChatItems.Items.Add(tb);
            ChatScroll.ScrollToEnd();
        }
    }
}
