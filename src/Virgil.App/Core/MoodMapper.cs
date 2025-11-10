using Virgil.App.Core;
using Virgil.App.Models;

namespace Virgil.App
{
    public class MoodMapper
    {
        public event Action<MoodState>? MoodChanged;

        public double WarnTemp { get; set; } = 80;
        public double AlertTemp { get; set; } = 90;
        public double WarnCpu { get; set; } = 90;

        public void OnMetrics(object? sender, MetricsEventArgs e)
        {
            var mood = MoodState.Focused;
            if (e.CpuTemp >= AlertTemp || e.GpuTemp >= AlertTemp) mood = MoodState.Alert;
            else if (e.CpuTemp >= WarnTemp || e.GpuTemp >= WarnTemp || e.CpuUsage >= WarnCpu) mood = MoodState.Warn;
            else if (e.CpuUsage <= 20 && e.RamUsage <= 30) mood = MoodState.Happy;
            MoodChanged?.Invoke(mood);
        }
    }
}
