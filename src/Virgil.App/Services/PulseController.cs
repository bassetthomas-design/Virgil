using System;
using System.Timers;
using Virgil.App.Services;
using Virgil.App.Chat;

namespace Virgil.App.Services
{
    public class PulseController
    {
        public event Action<double>? Pulse; // intensity 0..1
        private readonly MonitoringService _mon;
        private readonly ChatService _chat;
        private readonly Timer _cool = new(400) { AutoReset = false };

        public PulseController(MonitoringService mon, ChatService chat)
        {
            _mon = mon; _chat = chat;
            _mon.Metrics += OnMetrics;
            _chat.MessagePosted += (_, __, ___, ____) => Trigger(0.35);
        }

        private void OnMetrics(double cpuUsage, double gpuUsage, double ramUsage, double cpuTemp)
        {
            // simple heuristic: more load -> stronger pulse
            var intensity = Math.Clamp((cpuUsage + gpuUsage) / 200.0, 0.05, 0.9);
            Trigger(intensity * 0.8);
        }

        public void Trigger(double intensity)
        {
            // collapse bursts
            if (_cool.Enabled) return;
            _cool.Start();
            Pulse?.Invoke(Math.Clamp(intensity, 0, 1));
        }
    }
}
