// AUTO-PATCH: pass ChatService into MainViewModel ctor
using System.Windows;
using Virgil.App.Chat;
using Virgil.App.ViewModels;
using Virgil.App.Services;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var chat = new ChatService();

            var monitoringVm = new MonitoringViewModel(
                new MonitoringService(),
                new SettingsService(),
                new NetworkInsightService()
            );

            DataContext = new MainViewModel(chat, monitoringVm);
        }
    }
}
