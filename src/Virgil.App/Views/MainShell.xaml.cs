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

        public MainShell()
        {
            InitializeComponent();
            _chatVm = new ChatViewModel(_chat);
            _monVm = new MonitoringViewModel(_mon);
            _mon.Updated += _mood.OnMetrics;
            _monVm.HookMood(_mood);

            Chat.DataContext = _chatVm;
            Metrics.DataContext = _monVm;

            Loaded += (s,e) =>
            {
                _chat.Post("Virgil est en ligne.");
                _chat.Post("Surveillance activée.", MessageType.Success, pinned: true);
                _chat.Post("Petit message éphémère…", MessageType.Info, pinned: false, ttlMs: 3000);
            };
        }
    }
}
