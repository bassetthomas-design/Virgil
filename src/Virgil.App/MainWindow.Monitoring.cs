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
            _monitorTimer.Tick += (s, e) => RefreshSensors();
            _monitorTimer.Start();
        }

        private void StopMonitoring()
        {
            try { _monitorTimer.Stop(); } catch { }
        }

        private void RefreshSensors()
        {
            // TODO: MAJ CPU/RAM/GPU/Disk + températures via services
            // UI.CpuBar.Value = ...
        }
    }
}
