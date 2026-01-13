namespace Virgil.App.Models
{
    using System.ComponentModel;
    using Virgil.Core.Config;

    public enum AiProvider
    {
        EmbeddedLlama,
        OpenAI,
        Disabled
    }

    public class AppSettings
    {
        public int MonitoringIntervalMs { get; set; } = 7000;
        public int MonitoringIntervalMinutesMin { get; set; } = 5;
        public int MonitoringIntervalMinutesMax { get; set; } = 10;
        public int DefaultMessageTtlMs { get; set; } = 60000;
        public MoodThreshold Mood { get; set; } = new();
        public bool ShowMiniHud { get; set; } = true;
        public bool CompanionTalkative { get; set; } = false;
        public bool MonitoringEnabled { get; set; } = true;
        public bool EnableBeatPulse { get; set; } = true;
        public AiProvider? AiProvider { get; set; }
        public string OpenAiModel { get; set; } = "gpt-4o-mini";
        public int OpenAiTimeoutSeconds { get; set; } = 30;
        public string EmbeddedLlamaBaseUrl { get; set; } = "http://localhost:8080";
        public int EmbeddedLlamaTimeoutSeconds { get; set; } = 30;
        public bool AiPackFullEnabled { get; set; } = true;
        public string AiPackFullDownloadUrl { get; set; } = string.Empty;
        public string AiPackFullSha256 { get; set; } = string.Empty;
        public long? AiPackFullSizeBytes { get; set; }

        public ModelPackManifest GetActiveFullManifest()
        {
            var embedded = ModelPackManifest.FullPack;

            return embedded with
            {
                DownloadUrl = string.IsNullOrWhiteSpace(AiPackFullDownloadUrl) ? embedded.DownloadUrl : AiPackFullDownloadUrl,
                Sha256 = string.IsNullOrWhiteSpace(AiPackFullSha256) ? embedded.Sha256 : AiPackFullSha256,
                SizeBytes = AiPackFullSizeBytes ?? embedded.SizeBytes
            };
        }
    }

    public class MoodThreshold
    {
        public double WarnTemp { get; set; } = 70;
        public double AlertTemp { get; set; } = 85;
        public double WarnCpu { get; set; } = 85;
    }
}
