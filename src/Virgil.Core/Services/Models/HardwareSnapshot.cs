namespace Virgil.Core.Services
{
    /// <summary>
    /// Mesures matérielles "instantanées".
    /// Les valeurs peuvent être null si non disponibles sur la machine.
    /// </summary>
    public sealed class HardwareSnapshot
    {
        public float? CpuTempC { get; set; }
        public float? GpuTempC { get; set; }
        public float? DiskTempC { get; set; }
    }
}
