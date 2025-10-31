using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Horloge en haut à droite
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, __) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            // État initial du toggle surveillance
            UpdateSurveillanceLabel();
        }

        // ===== Helpers =====
        private void AppendChat(string message)
        {
            var bubble = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            };
            ChatItems.Items.Add(bubble);
            ChatScroll.ScrollToEnd();
        }

        private void UpdateSurveillanceLabel()
        {
            // Le XAML utilise: Content="{DynamicResource SurveillanceToggleText}"
            // On met à jour la Resource pour refléter l'état courant.
            var text = (SurveillanceToggle.IsChecked == true)
                ? "Arrêter la surveillance"
                : "Démarrer la surveillance";
            Resources["SurveillanceToggleText"] = text;
        }

        // ===== Handlers du Toggle surveillance (déclarés dans XAML) =====
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSurveillanceLabel();
            AppendChat("Surveillance activée.");
            // TODO: démarrer un timer de télémétrie CPU/GPU/RAM/Disk si nécessaire
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateSurveillanceLabel();
            AppendChat("Surveillance désactivée.");
            // TODO: arrêter le timer de télémétrie si nécessaire
        }

        // ===== Handlers des boutons d'action (déclarés dans XAML) =====
        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            AppendChat("Maintenance complète lancée… (simulation)");
            await Dispatcher.InvokeAsync(() => { /* TODO: appeler VirgilService.FullMaintenanceAsync() */ });
            AppendChat("Maintenance complète terminée. (simulation)");
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            AppendChat("Nettoyage intelligent lancé… (simulation)");
            await Dispatcher.InvokeAsync(() => { /* TODO: VirgilService.CleanSmartAsync() */ });
            AppendChat("Nettoyage intelligent terminé. (simulation)");
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            AppendChat("Nettoyage des navigateurs lancé… (simulation)");
            await Dispatcher.InvokeAsync(() => { /* TODO: VirgilService.CleanCustomAsync(preset:browsers) */ });
            AppendChat("Nettoyage des navigateurs terminé. (simulation)");
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            AppendChat("Mises à jour complètes lancées… (simulation)");
            await Dispatcher.InvokeAsync(() => { /* TODO: VirgilService.UpdateAsync(all:true) */ });
            AppendChat("Mises à jour complètes terminées. (simulation)");
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            AppendChat("Defender (MAJ + Scan complet) lancé… (simulation)");
            await Dispatcher.InvokeAsync(() => { /* TODO: VirgilService.DefenderFullScanAsync() */ });
            AppendChat("Defender (MAJ + Scan complet) terminé. (simulation)");
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            AppendChat("Ouverture de la fenêtre de paramètres…");
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }
    }
}
