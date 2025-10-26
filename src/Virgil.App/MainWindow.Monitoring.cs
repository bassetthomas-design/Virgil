using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Snap = Virgil.Core.Monitoring.HardwareSnapshot;

namespace Virgil.App
{
    public partial class MainWindow
    {
        private readonly Brush _ok    = new SolidColorBrush(Color.FromRgb(0x54, 0xC5, 0x6C));
        private readonly Brush _warn  = new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F));
        private readonly Brush _alert = new SolidColorBrush(Color.FromRgb(0xD9, 0x3D, 0x3D));

        /// <summary>
        /// Tick de surveillance (appelé par _survTimer dans MainWindow.xaml.cs).
        /// </summary>
        private async System.Threading.Tasks.Task SurveillancePulseInternal()
        {
            try
            {
                var snap = await _monitor.GetSnapshotAsync();

                SetGauge(CpuBar,  snap.CpuUsage,  T.CpuWarn,  T.CpuAlert,  CpuBadge,  CpuBadgeHot,
                         $"CPU: {snap.CpuUsage:F0}% — {(double.IsNaN(snap.CpuTemp) ? "--" : $"{snap.CpuTemp:F0}°C")}");
                CpuTemp.Text = double.IsNaN(snap.CpuTemp) ? "-- °C" : $"{snap.CpuTemp:F1} °C";

                SetGauge(GpuBar,  snap.GpuUsage,  T.GpuWarn,  T.GpuAlert,  GpuBadge,  GpuBadgeHot,
                         $"GPU: {snap.GpuUsage:F0}% — {(double.IsNaN(snap.GpuTemp) ? "--" : $"{snap.GpuTemp:F0}°C")}");
                GpuTemp.Text = double.IsNaN(snap.GpuTemp) ? "-- °C" : $"{snap.GpuTemp:F1} °C";

                SetGauge(MemBar,  snap.MemUsage,  T.MemWarn,  T.MemAlert,  MemBadge,  MemBadgeHot,
                         $"Mémoire: {snap.MemUsage:F0}%");

                SetGauge(DiskBar, snap.DiskUsage, T.DiskWarn, T.DiskAlert, DiskBadge, DiskBadgeHot,
                         $"Disque: {snap.DiskUsage:F0}% — {(double.IsNaN(snap.DiskTemp) ? "--" : $"{snap.DiskTemp:F0}°C")}");
                DiskTemp.Text = double.IsNaN(snap.DiskTemp) ? "-- °C" : $"{snap.DiskTemp:F1} °C";

                EvaluateAndReact(snap);
                StatusText.Text = $"Pulse @ {DateTime.Now:HH:mm:ss}";

                if (DateTime.Now >= _nextPunch)
                {
                    Say(GetRandomPunchline(), Mood.Playful);
                    PlanNextPunchline();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Erreur surveillance: " + ex.Message;
            }
        }

        private void SetGauge(
            System.Windows.Controls.ProgressBar bar, double value, int warn, int alert,
            TextBlock badgeWarn, TextBlock badgeHot, string tooltip)
        {
            bar.Value = value;
            bar.ToolTip = tooltip;

            if (value >= alert)
            {
                bar.Foreground = _alert;
                badgeHot.Visibility = Visibility.Visible;
                badgeWarn.Visibility = Visibility.Collapsed;
            }
            else if (value >= warn)
            {
                bar.Foreground = _warn;
                badgeWarn.Visibility = Visibility.Visible;
                badgeHot.Visibility = Visibility.Collapsed;
            }
            else
            {
                bar.Foreground = _ok;
                badgeWarn.Visibility = Visibility.Collapsed;
                badgeHot.Visibility = Visibility.Collapsed;
            }
        }
    }
}
