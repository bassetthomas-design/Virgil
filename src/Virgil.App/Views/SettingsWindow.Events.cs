using System.Windows;
using System.Windows.Controls;

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

        private void OnOpenAiKeyPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _vm.OpenAiApiKeyInput = passwordBox.Password;
            }
        }

        private void OnOpenAiKeyVisibilityChanged(object sender, RoutedEventArgs e)
        {
            if (OpenAiKeyBox is not null)
            {
                OpenAiKeyBox.Password = _vm.OpenAiApiKeyInput ?? string.Empty;
            }
        }
    }
}
