using System.Windows;
using Virgil.App.Chat;
using Virgil.App.ViewModels;
using Virgil.App.Services;

namespace Virgil.App.Views
{
    public partial class MainShell : Window
    {
        private readonly ChatService _chat = new();
        private readonly MonitoringService _mon = new();
        private readonly MonitoringViewModel _monVm;
        private readonly ChatViewModel _chatVm;
        private readonly MoodMapper _mood = new();
        private readonly SettingsService _settings = new();
        private readonly ActionsService _actionsSvc = new();
        private readonly ActionsViewModel _actionsVm;

        public MainShell()
        {
            InitializeComponent();
            _chatVm = new ChatViewModel(_chat);
            _monVm = new MonitoringViewModel(_mon);
            _mon.Updated += _mood.OnMetrics;
            _monVm.HookMood(_mood);

            _actionsVm = new ActionsViewModel(_actionsSvc, _chat, _mon, _settings);

            Chat.DataContext = _chatVm;
            Metrics.DataContext = _monVm;
            Actions.DataContext = _actionsVm;

            Loaded += (s,e) =>
            {
                _chat.Post("Virgil est en ligne.");
                _chat.Post("Surveillance activée.", MessageType.Success, pinned: true);
                _chat.Post("Petit message éphémère…", MessageType.Info, pinned: false, ttlMs: 3000);
            };
        }

        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_settings);
            if (dlg.ShowDialog() == true)
            {
                // TODO: appliquer dynamiquement intervalle/thresholds/TTL si besoin
            }
        }
    }
}
