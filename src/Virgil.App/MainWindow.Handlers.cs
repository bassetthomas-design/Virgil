using System.Windows;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire to ServiceManager full maintenance
        }

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire to CleaningService
        }

        private void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire to BrowserCleaningService
        }

        private void UpdateAllButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: wire to UpdateService / DriverUpdateService / WindowsUpdateService
        }
    }
}
