using System;
using System.Threading;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private void StartMonitoring()
        {
            // Démarre le service de monitoring bas niveau (compteurs Windows + LHM).
            if (_systemMonitorService is not null)
            {
                _ = _systemMonitorService.StartAsync(CancellationToken.None);
            }
            else
            {
                _legacyMonitoringService?.Start();
            }
        }

        private void StopMonitoring()
        {
            if (_systemMonitorService is not null)
            {
                _ = _systemMonitorService.StopAsync(CancellationToken.None);
            }
            else
            {
                _legacyMonitoringService?.Stop();
            }
        }
    }
}
