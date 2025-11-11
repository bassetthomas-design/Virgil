namespace Virgil.App.Models
{
    public class AppSettings
    {
        public int MonitoringIntervalMs { get; set; } = 2000;
        public int DefaultMessageTtlMs { get; set; } = 60000;
        public MoodThreshold Mood { get; set; } = new();
        public bool ShowMiniHud { get; set; } = true;
        public bool CompanionTalkative { get; set; } = false;
        public bool EnableBeatPulse { get; set; } = true;
    }

    public class MoodThreshold
    {
        public double WarnTemp { get; set; } = 70;
        public double AlertTemp { get; set; } = 85;
        public double WarnCpu { get; set; } = 85;
    }
}
