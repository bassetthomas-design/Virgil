using System.Windows;
using System.Windows.Controls;
using Virgil.App.ViewModels;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly DashboardViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new DashboardViewModel();
            DataContext = _vm;
        }

        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
            => _vm.ToggleSurveillance();

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
            => _vm.ToggleSurveillance();

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
            => _vm.RunMaintenance();

        private void Action_CleanTemp(object sender, RoutedEventArgs e)
            => _vm.CleanTempFiles();

        private void Action_CleanBrowsers(object sender, RoutedEventArgs e)
            => _vm.CleanBrowsers();

        private void Action_UpdateAll(object sender, RoutedEventArgs e)
            => _vm.UpdateAll();

        private void Action_Defender(object sender, RoutedEventArgs e)
            => _vm.RunDefenderScan();

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
            => _vm.OpenConfiguration();

        // Exemple de message — fully qualified pour lever l’ambiguïté
        private void ShowInfo(string text)
            => System.Windows.MessageBox.Show(this, text, "Virgil", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
