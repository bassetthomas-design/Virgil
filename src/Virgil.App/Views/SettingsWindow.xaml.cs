using System.Windows;
using Virgil.App.Chat;
using Virgil.App.Services;
using Virgil.App.ViewModels;
using Virgil.Services.Assistant;

namespace Virgil.App.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _vm;
        public SettingsWindow(SettingsService svc, ChatService? chatService = null, IAssistantService? assistantService = null)
        {
            InitializeComponent();
            _vm = new SettingsViewModel(svc, chatService, assistantService);
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
