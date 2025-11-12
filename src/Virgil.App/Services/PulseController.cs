using System;
using System.Timers;
using Virgil.App.Chat;
using Virgil.App.ViewModels;
using Virgil.App.Core;

namespace Virgil.App.Services
{
    /// <summary>
    /// Listens to chat messages and briefly adjusts the avatar mood.
    /// </summary>
    public sealed class PulseController : IDisposable
    {
        private readonly MonitoringViewModel _monitoring;
        private readonly Timer _recovery;

        public PulseController(ChatService chat, MonitoringViewModel monitoring)
        {
            _monitoring = monitoring;
            _recovery = new Timer(1500) { AutoReset = false };
            _recovery.Elapsed += OnRecovery;

            chat.MessagePosted += OnMessagePosted;
        }

        private void OnMessagePosted(object sender, string text, ChatKind kind, int? ttlMs)
        {
            // Map chat kind to a short-lived mood pulse
            _recovery.Stop();

            _monitoring.CurrentMood = kind switch
            {
                ChatKind.Error => Mood.Angry,
                ChatKind.Warning => Mood.Tired,
                _ => Mood.Happy
            };

            _recovery.Interval = ttlMs.HasValue && ttlMs.Value > 0 ? ttlMs.Value : 1500;
            _recovery.Start();
        }

        private void OnRecovery(object? s, ElapsedEventArgs e)
        {
            _monitoring.CurrentMood = Mood.Neutral;
        }

        public void Dispose()
        {
            _recovery.Elapsed -= OnRecovery;
            _recovery.Dispose();
        }
    }
}
