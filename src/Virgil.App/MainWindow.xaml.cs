using MessageBox = System.Windows.MessageBox; // évite l’ambiguïté Forms/WPF
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

// Alias explicite pour lever l’ambiguïté MessageBox (Forms vs WPF)
using WpfMessageBox = System.Windows.MessageBox;

using Virgil.App.ViewModels;

namespace Virgil.App
{
    public partial class MainWindow : Window, System.Windows.Markup.IComponentConnector
    {
        private DashboardViewModel Vm => (DashboardViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
        }

        // =========================
        // Handlers des boutons/toggles
        // =========================

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                Vm.ToggleSurveillance();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Surveillance", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                Vm.ToggleSurveillance();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Surveillance", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            try { await Vm.RunMaintenance(); }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Maintenance complète", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            try { await Vm.CleanTempFiles(); }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Nettoyage temporaire", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            try { await Vm.CleanBrowsers(); }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Navigateurs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            try { await Vm.UpdateAll(); }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Mises à jour", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            try { await Vm.RunDefenderScan(); }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Microsoft Defender", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            try { Vm.OpenConfiguration(); }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                WpfMessageBox.Show(this, ex.Message, "Configuration", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
