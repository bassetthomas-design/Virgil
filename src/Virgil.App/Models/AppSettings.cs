namespace Virgil.App.Models
{
    using System.ComponentModel;

    public enum AiProvider
    {
        EmbeddedLlama,
        OpenAI,
        Disabled
    }

    public class AppSettings
    {
        public int MonitoringIntervalMs { get; set; } = 2000;
        public int DefaultMessageTtlMs { get; set; } = 60000;
        public MoodThreshold Mood { get; set; } = new();
        public bool ShowMiniHud { get; set; } = true;
        public bool CompanionTalkative { get; set; } = false;
        public bool MonitoringEnabled { get; set; } = true;
        public bool EnableBeatPulse { get; set; } = true;
        public AiProvider AiProvider { get; set; } = AiProvider.EmbeddedLlama;
        public string OpenAiModel { get; set; } = "gpt-4o-mini";
        public int OpenAiTimeoutSeconds { get; set; } = 30;
        public string EmbeddedLlamaBaseUrl { get; set; } = "http://localhost:8080";
        public int EmbeddedLlamaTimeoutSeconds { get; set; } = 30;
    }

    public class MoodThreshold
    {
        public double WarnTemp { get; set; } = 70;
        public double AlertTemp { get; set; } = 85;
        public double WarnCpu { get; set; } = 85;
    }
}
