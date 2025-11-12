using Virgil.App.Chat;
using Virgil.App.Services;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel
    {
        private readonly PulseController _pulse;

        public MainViewModel(ChatService chat, MonitoringViewModel monitoring)
        {
            _pulse = new PulseController(chat, monitoring);
        }
    }
}
