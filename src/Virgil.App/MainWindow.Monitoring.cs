using System;
using System.Threading.Tasks;
using System.Windows;
using Virgil.Core.Monitoring;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly AdvancedMonitoringService _monitor = new();

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

            // alertes seuils (à compléter avec ConfigService)
            if (snap.CpuUsage > 90 || snap.GpuUsage > 90 || snap.MemUsage > 95)
                Say("⚠️ Charge élevée détectée. Pense à une pause café.", Mood.Alert);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Erreur de lecture: " + ex.Message;
        }
    }
}
