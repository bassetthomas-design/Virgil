using WpfMessageBox = System.Windows.MessageBox;
using System;
using System.Windows;
using System.Windows.Threading;
using Virgil.App.ViewModels;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer = new();
        private readonly DashboardViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            if (UI != null)
            {
                UI.SurveillanceToggle.Checked   += SurveillanceToggle_Checked;
                UI.SurveillanceToggle.Unchecked += SurveillanceToggle_Unchecked;

                UI.BtnMaintenance.Click   += Action_MaintenanceComplete;
                UI.BtnCleanTemp.Click     += Action_CleanTemp;
                UI.BtnCleanBrowsers.Click += Action_CleanBrowsers;
                UI.BtnUpdateAll.Click     += Action_UpdateAll;
                UI.BtnDefender.Click      += Action_Defender;
                UI.BtnOpenConfig.Click    += OpenConfig_Click;
            }
            else
            {
                MessageBox.Show(
                    "Erreur : Interface principale non initialisée (UI null).",
                    "Virgil",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) =>
            {
                if (UI?.ClockText != null)
                    UI.ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            _clockTimer.Start();
        }

        // --- Gestion des événements ---

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Arrêter la surveillance";
            _viewModel.ToggleSurveillance(true);
            AddChat("Surveillance activée.");
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            Resources["SurveillanceToggleText"] = "Démarrer la surveillance";
            _viewModel.ToggleSurveillance(false);
            AddChat("Surveillance arrêtée.");
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            AddChat("Mode maintenance activé…");
            _viewModel.RunMaintenance();
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            AddChat("Nettoyage intelligent en cours…");
            _viewModel.CleanTempFiles();
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            AddChat("Nettoyage navigateurs…");
            _viewModel.CleanBrowsers();
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            AddChat("Mises à jour totales (apps/jeux/pilotes/Windows/Defender)…");
            _viewModel.UpdateAll();
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            AddChat("Microsoft Defender (MAJ + Scan rapide)…");
            _viewModel.RunDefenderScan();
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            AddChat("Ouverture de la configuration…");
            _viewModel.OpenConfiguration();
        }

        private void AddChat(string text)
        {
            UI.ChatItems.Items.Add(text);
            UI.ChatScroll.ScrollToEnd();
        }
    }
}
