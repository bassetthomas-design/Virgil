using System;
using System.Threading.Tasks;
using System.Windows;
using Virgil.Core.Monitoring;
using Virgil.Core.Config;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly AdvancedMonitoringService _monitor = new();
    private readonly ConfigService _config = new();
    private Thresholds _t => _config.Get().Thresholds;

    private async void SurveillancePulse()
    {
        try
        {
            var snap = await _monitor.GetSnapshotAsync();
            CpuBar.Value = snap.CpuUsage;
            GpuBar.Value = snap.GpuUsage;
            MemBar.Value = snap.MemUsage;
            DiskBar.Value = snap.DiskUsage;

            CpuTemp.Text = double.IsNaN(snap.CpuTemp) ? "-- °C" : $"{snap.CpuTemp:F1} °C";
            GpuTemp.Text = double.IsNaN(snap.GpuTemp) ? "-- °C" : $"{snap.GpuTemp:F1} °C";
            DiskTemp.Text = double.IsNaN(snap.DiskTemp) ? "-- °C" : $"{snap.DiskTemp:F1} °C";

            EvaluateAndReact(snap);
            StatusText.Text = $"Pulse @ {DateTime.Now:HH:mm:ss}";

            if(DateTime.Now >= _nextPunch){
                Say(GetRandomPunchline(), Mood.Playful);
                PlanNextPunchline();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erreur de lecture: " + ex.Message;
        }
    }
}
