using System;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private void StartMonitoring()
        {
            // Démarre le service de monitoring bas niveau (compteurs Windows + LHM).
            _monitoringService.Start();
        }

        private void StopMonitoring()
        {
            _monitoringService.Stop();
        }
    }
}
