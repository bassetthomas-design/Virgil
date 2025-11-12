using System.Timers;
using Virgil.App.Chat;
using Virgil.App.ViewModels;
using Virgil.App.Core;

namespace Virgil.App.Services
{
    public class PulseController
    {
        private readonly MonitoringViewModel _monitoring;
        private readonly Timer _recovery = new(1500) { AutoReset = false };

        public PulseController(ChatService chat, MonitoringViewModel monitoring)
        {
            _monitoring = monitoring;
            chat.MessagePosted += OnMessage;
            _recovery.Elapsed += (_, __) => _monitoring.CurrentMood = Mood.Neutral;
        }

        private void OnMessage(object sender, string text, ChatKind kind, int? ttlMs)
        {
            _recovery.Stop();
            _monitoring.CurrentMood = kind switch
            {
                ChatKind.Error   => Mood.Angry,
                ChatKind.Warning => Mood.Tired,
                _                => Mood.Happy
            };
            _recovery.Interval = ttlMs is > 0 ? ttlMs.Value : 1500;
            _recovery.Start();
        }
    }
}
