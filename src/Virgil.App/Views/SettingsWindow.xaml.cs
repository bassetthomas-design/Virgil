using System.Windows;
using Virgil.App.Services;
using Virgil.App.ViewModels;

namespace Virgil.App.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _vm;
        public SettingsWindow(SettingsService svc)
        {
            InitializeComponent();
            _vm = new SettingsViewModel(svc);
            DataContext = _vm;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            _vm.Save();
            DialogResult = true;
            Close();
        }
    }
}
