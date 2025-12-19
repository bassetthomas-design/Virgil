using Virgil.App.Chat;
using Virgil.App.Services;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel
    {
        private readonly PulseController _pulse;

        public MonitoringViewModel Monitoring { get; }
        public ChatViewModel Chat { get; }
        public ActionsViewModel? Actions { get; }
        public string StatusText { get; } = "Virgil est prêt";

        public MainViewModel(ChatService chat, MonitoringViewModel monitoring)
        {
            Monitoring = monitoring;
            Chat = new ChatViewModel(chat);
            _pulse = new PulseController(chat, monitoring);
            
            // Actions is optional for now since it requires more dependencies
            // It can be added later when needed
            Actions = null;
        }
    }
}
