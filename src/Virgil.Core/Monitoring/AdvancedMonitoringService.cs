using System.Threading.Tasks;

namespace Virgil.Core.Monitoring
{
    /// <summary>
    /// Service de monitoring (stub). Retourne un snapshot vide pour permettre la compilation.
    /// Brancher la vraie collecte plus tard (perf counters, capteurs, etc.).
    /// </summary>
    public sealed class AdvancedMonitoringService
    {
        public Task<HardwareSnapshot> GetSnapshotAsync()
        {
            // TODO: Implémentation réelle CPU/GPU/Mémoire/Disque + températures
            var snap = new HardwareSnapshot
            {
                CpuUsage = 0,
                GpuUsage = 0,
                MemUsage = 0,
                DiskUsage = 0,
                CpuTemp = double.NaN,
                GpuTemp = double.NaN,
                DiskTemp = double.NaN
            };
            return Task.FromResult(snap);
        }
    }
}
