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

            // Initialisation du ViewModel
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            // Vérifie que le UserControl UI est bien chargé avant d'accéder à ses éléments
            if (UI != null)
            {
                // Connexion des événements
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

            // Horloge temps réel
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) =>
            {
                if (UI?.ClockText != null)
                    UI.ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            _clockTimer.Start();
        }

        // --- Gestion des événements d'interface ---

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleSurveillance(true);
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleSurveillance(false);
        }

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            _viewModel.RunMaintenance();
        }

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            _viewModel.CleanTempFiles();
        }

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            _viewModel.CleanBrowsers();
        }

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            _viewModel.UpdateAll();
        }

        private void Action_Defender(object sender, RoutedEventArgs e)
        {
            _viewModel.RunDefenderScan();
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.OpenConfiguration();
        }
    }
}
