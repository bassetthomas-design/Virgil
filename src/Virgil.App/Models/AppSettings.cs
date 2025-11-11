namespace Virgil.App.Models
{
    public class AppSettings
    {
        public int MonitoringIntervalMs { get; set; } = 2000;
        public int DefaultMessageTtlMs { get; set; } = 60000;
        public MoodThreshold Mood { get; set; } = new();
        // Persist Mini HUD state
        public bool ShowMiniHud { get; set; } = true;
        // Controls how chatty the companion mode is
        public bool CompanionTalkative { get; set; } = false;
    }

    public class MoodThreshold
    {
        public double WarnTemp { get; set; } = 70;
        public double AlertTemp { get; set; } = 85;
        public double WarnCpu { get; set; } = 85;
    }
}
