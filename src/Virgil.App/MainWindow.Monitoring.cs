using System;
using System.Threading;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private void StartMonitoring()
        {
            // Démarre le service de monitoring bas niveau (compteurs Windows + LHM).
            _ = _systemMonitorService.StartAsync(CancellationToken.None);
        }

        private void StopMonitoring()
        {
            _ = _systemMonitorService.StopAsync(CancellationToken.None);
        }
    }
}
