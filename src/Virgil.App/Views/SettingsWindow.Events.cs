using System.Windows;

namespace Virgil.App.Views
{
    public partial class SettingsWindow : Window
    {
        private void OnOk(object sender, RoutedEventArgs e)
        {
            // Close the dialog confirming changes. Settings saving is handled elsewhere.
            DialogResult = true;
            Close();
        }
    }
}
