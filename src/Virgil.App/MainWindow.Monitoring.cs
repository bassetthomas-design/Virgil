using System;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private readonly DispatcherTimer _monitorTimer = new();

        private void StartMonitoring()
        {
            _monitorTimer.Interval = TimeSpan.FromSeconds(2);
            _monitorTimer.Tick += (s, e) =>
            {
                // TODO : lecture CPU/GPU/RAM/Températures
                // Actualise les jauges + émotions dans le ViewModel
            };
            _monitorTimer.Start();
        }

        private void StopMonitoring()
        {
            _monitorTimer.Stop();
        }
    }
}
