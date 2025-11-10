using System;

namespace Virgil.App.Models
{
    public class MoodThresholds
    {
        public double WarnTemp { get; set; } = 80;
        public double AlertTemp { get; set; } = 90;
        public double WarnCpu { get; set; } = 90;
    }

    public class AppSettings
    {
        public int MonitoringIntervalMs { get; set; } = 2000;
        public int DefaultMessageTtlMs { get; set; } = 60000;
        public bool CompanionTalkative { get; set; } = true;
        public MoodThresholds Mood { get; set; } = new();
    }
}
