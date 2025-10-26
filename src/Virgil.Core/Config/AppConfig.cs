using System.Text.Json;
using System.Text.Json.Serialization;

namespace Virgil.Core.Config
{
    public sealed class AppConfig
    {
        public double CpuWarn { get; set; } = 75;
        public double CpuAlert { get; set; } = 90;
        public double MemWarn { get; set; } = 75;
        public double MemAlert { get; set; } = 90;
        public double GpuTempWarn { get; set; } = 80;
        public double GpuTempAlert { get; set; } = 90;
        public double CpuTempWarn { get; set; } = 80;
        public double CpuTempAlert { get; set; } = 90;

        public bool TelemetryOptIn { get; set; } = false; // réservé, pas utilisé

        [JsonIgnore] public static JsonSerializerOptions JsonOptions => new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
