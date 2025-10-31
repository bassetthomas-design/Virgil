using System.Windows;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e) => StartSurveillance();
        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e) => StopSurveillance();

        private void Action_MaintenanceComplete(object sender, RoutedEventArgs e) => RunMaintenanceAsync();
        private void Action_CleanTemp(object sender, RoutedEventArgs e) => CleanSmartAsync();
        private void Action_CleanBrowsers(object sender, RoutedEventArgs e) => CleanBrowsersAsync();
        private void Action_UpdateAll(object sender, RoutedEventArgs e) => UpdateAllAsync();
        private void Action_Defender(object sender, RoutedEventArgs e) => DefenderAsync();
        private void OpenConfig_Click(object sender, RoutedEventArgs e) => OpenConfig();
    }
}
