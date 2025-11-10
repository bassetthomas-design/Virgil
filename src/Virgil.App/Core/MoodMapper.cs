using System;
using Virgil.App.Core;
using Virgil.App.Models;

namespace Virgil.App
{
    public class MoodMapper
    {
        public event Action<MoodState>? MoodChanged;

        public void OnMetrics(object? sender, MetricsEventArgs e)
        {
            // Simple rules: prioritize temperature alerts, then CPU
            var mood = MoodState.Focused;
            if (e.CpuTemp >= 90 || e.GpuTemp >= 90) mood = MoodState.Alert;
            else if (e.CpuTemp >= 80 || e.GpuTemp >= 80) mood = MoodState.Warn;
            else if (e.CpuUsage >= 90) mood = MoodState.Warn;
            else if (e.CpuUsage <= 20 && e.RamUsage <= 30) mood = MoodState.Happy;

            MoodChanged?.Invoke(mood);
        }
    }
}
