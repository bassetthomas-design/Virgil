using System;
using System.Timers;
using Virgil.App.Chat;
using Virgil.App.ViewModels;
using Virgil.App.Core;

namespace Virgil.App.Services
{
    public class PulseController : IDisposable
    {
        private readonly MonitoringViewModel _monitoring;
        private readonly Timer _recovery;

        public PulseController(ChatService chat, MonitoringViewModel monitoring)
        {
            _monitoring = monitoring;
            _recovery = new Timer(1500) { AutoReset = false };
            _recovery.Elapsed += (_, __) => _monitoring.CurrentMood = default; // Neutral fallback

            chat.MessagePosted += OnMessage;
        }

        private void OnMessage(object sender, string text, ChatKind kind, int? ttlMs)
        {
            _recovery.Stop();
            // Safe fallback to neutral until Core.Mood canonical set is finalized
            _monitoring.CurrentMood = default;
            _recovery.Interval = ttlMs.GetValueOrDefault(1500);
            _recovery.Start();
        }

        public void Dispose() => _recovery.Dispose();
    }
}
