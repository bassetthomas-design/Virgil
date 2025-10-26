using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Virgil.Core.Monitoring;
using Virgil.Core.Config;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly AdvancedMonitoringService _monitor = new();
    private readonly ConfigService _config = new();
    private Thresholds _t => _config.Get().Thresholds;

    private Brush OkBrush    = new SolidColorBrush(Color.FromRgb(0x54,0xC5,0x6C));
    private Brush WarnBrush  = new SolidColorBrush(Color.FromRgb(0xF1,0xC4,0x0F));
    private Brush AlertBrush = new SolidColorBrush(Color.FromRgb(0xD9,0x3D,0x3D));

    private async void SurveillancePulse()
    {
        try
        {
            var snap = await _monitor.GetSnapshotAsync();
            SetGauge(CpuBar, snap.CpuUsage, _t.CpuWarn, _t.CpuAlert, CpuBadge, CpuBadgeHot, $"CPU: {snap.CpuUsage:F0}% — {(double.IsNaN(snap.CpuTemp)?"--":$"{snap.CpuTemp:F0}°C")}");
            SetGauge(GpuBar, snap.GpuUsage, _t.GpuWarn, _t.GpuAlert, GpuBadge, GpuBadgeHot, $"GPU: {snap.GpuUsage:F0}% — {(double.IsNaN(snap.GpuTemp)?"--":$"{snap.GpuTemp:F0}°C")}");
            SetGauge(MemBar, snap.MemUsage, _t.MemWarn, _t.MemAlert, MemBadge, MemBadgeHot, $"Mémoire: {snap.MemUsage:F0}%");
            SetGauge(DiskBar, snap.DiskUsage, _t.DiskWarn, _t.DiskAlert, DiskBadge, DiskBadgeHot, $"Disque: {snap.DiskUsage:F0}% — {(double.IsNaN(snap.DiskTemp)?"--":$"{snap.DiskTemp:F0}°C")}");

            CpuTemp.Text  = double.IsNaN(snap.CpuTemp)  ? "-- °C" : $"{snap.CpuTemp:F1} °C";
            GpuTemp.Text  = double.IsNaN(snap.GpuTemp)  ? "-- °C" : $"{snap.GpuTemp:F1} °C";
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

    private void SetGauge(ProgressBar bar, double value, int warn, int alert, TextBlock badgeWarn, TextBlock badgeHot, string tooltip){
        bar.Value = value;
        bar.ToolTip = tooltip;
        if (value >= alert){ bar.Foreground = AlertBrush; badgeHot.Visibility = Visibility.Visible; badgeWarn.Visibility = Visibility.Collapsed; }
        else if (value >= warn){ bar.Foreground = WarnBrush; badgeWarn.Visibility = Visibility.Visible; badgeHot.Visibility = Visibility.Collapsed; }
        else{ bar.Foreground = OkBrush; badgeWarn.Visibility = Visibility.Collapsed; badgeHot.Visibility = Visibility.Collapsed; }
    }
}
