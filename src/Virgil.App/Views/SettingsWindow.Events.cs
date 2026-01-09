using System.Windows;

namespace Virgil.App.Views
{
    public partial class SettingsWindow : Window
    {
        private void OnOk(object sender, RoutedEventArgs e)
        {
            _vm.Save();
            DialogResult = true;
            Close();
        }
    }
}
