using System;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private readonly DispatcherTimer _monitorTimer = new DispatcherTimer();

        private void StartMonitoring()
        {
            _monitorTimer.Interval = TimeSpan.FromSeconds(2);
            _monitorTimer.Tick += (s, e) => RefreshMetrics();
            _monitorTimer.Start();
        }

        private void StopMonitoring()
        {
            _monitorTimer.Stop();
        }

        private readonly Random _rng = new Random();

        private void RefreshMetrics()
        {
            // Placeholder : valeurs aléatoires pour vérifier le binding UI
            CpuBar.Value  = _rng.Next(0, 100);
            RamBar.Value  = _rng.Next(0, 100);
            GpuBar.Value  = _rng.Next(0, 100);
            DiskBar.Value = _rng.Next(0, 100);
        }
    }
}
