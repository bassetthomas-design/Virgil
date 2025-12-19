using Virgil.App.Chat;
using Virgil.App.Services;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel
    {
        private readonly PulseController _pulse;

        public MonitoringViewModel Monitoring { get; }
        public ChatViewModel Chat { get; }
        public ActionsViewModel Actions { get; }
        public string StatusText { get; } = "Virgil est prêt";

        public MainViewModel(ChatService chat, MonitoringViewModel monitoring, ActionsViewModel actions)
        {
            Monitoring = monitoring;
            Chat = new ChatViewModel(chat);
            Actions = actions;
            _pulse = new PulseController(chat, monitoring);
        }
    }
}
