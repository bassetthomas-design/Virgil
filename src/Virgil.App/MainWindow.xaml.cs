using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Virgil.Core.Services; // ajustez selon votre namespace

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _surveillanceTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Horloge en haut à droite
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, __) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            // Timer de surveillance (mettez à jour selon vos besoins)
            _surveillanceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _surveillanceTimer.Tick += (_, __) => { /* TODO: mise à jour CPU/RAM/GPU/DISK */ };

            UpdateSurveillanceLabel();
        }

        private void AppendChat(string message)
        {
            ChatItems.Items.Add(new TextBlock { Text = message, Margin = new Thickness(0,0,0,6), TextWrapping = TextWrapping.Wrap });
            ChatScroll.ScrollToEnd();
        }

        private void UpdateSurveillanceLabel()
        {
            Resources["SurveillanceToggleText"] = (SurveillanceToggle.IsChecked == true)
                ? "Arrêter la surveillance"
                : "Démarrer la surveillance";
        }

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            _surveillanceTimer.Start();
            UpdateSurveillanceLabel();
            AppendChat("Surveillance activée.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _surveillanceTimer.Stop();
            UpdateSurveillanceLabel();
            AppendChat("Surveillance désactivée.");
        }

        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            AppendChat("Maintenance complète lancée…");
            var report = await VirgilService.FullMaintenanceAsync();  // utilise votre service
            AppendChat(report);
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            AppendChat("Nettoyage intelligent lancé…");
            var report = await VirgilService.CleanSmartAsync();
            AppendChat(report);
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            AppendChat("Nettoyage des navigateurs lancé…");
            var report = await VirgilService.CleanCustomAsync(cleanTemp: false, cleanBrowsers: true, cleanExtended: false);
            AppendChat(report);
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            AppendChat("Mises à jour complètes lancées…");
            var report = await VirgilService.UpdateAsync(); // lance MAJ apps/Windows/etc.
            AppendChat(report);
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            AppendChat("Defender (MAJ + Scan) lancé…");
            var report = await VirgilService.DefenderFullScanAsync();
            AppendChat(report);
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            AppendChat("Ouverture de la fenêtre de paramètres…");
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }
    }
}
