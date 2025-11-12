using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel
    {
        public MonitoringViewModel Monitoring { get; } = new();
        private readonly PulseController _pulse;

        public MainViewModel(ChatService chat)
        {
            _pulse = new PulseController(chat, Monitoring);
        }
    }
}
