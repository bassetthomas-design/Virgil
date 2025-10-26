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

        // Helpers pour éviter les interpolations imbriquées (qui ont été cassées)
        private static string TempShort(double t) => double.IsNaN(t) ? "--" : $"{t:F0}°C";
        private static string TempFull(double t)  => double.IsNaN(t) ? "-- °C" : $"{t:F1} °C";

        private async System.Threading.Tasks.Task SurveillancePulseInternal()
        {
            try
            {
                var snap = await _monitor.GetSnapshotAsync();

                // CPU
                SetGauge(
                    CpuBar,
                    snap.CpuUsage,
                    T.CpuWarn,
                    T.CpuAlert,
                    CpuBadge,
                    CpuBadgeHot,
                    $"CPU: { (double.IsNaN(snap.CpuUsage) ? "--" : $"{snap.CpuUsage:F0}%") } — {TempShort(snap.CpuTemp)}"
                );
                CpuTemp.Text = TempFull(snap.CpuTemp);

                // GPU
                SetGauge(
                    GpuBar,
                    snap.GpuUsage,
                    T.GpuWarn,
                    T.GpuAlert,
                    GpuBadge,
                    GpuBadgeHot,
                    $"GPU: { (double.IsNaN(snap.GpuUsage) ? "--" : $"{snap.GpuUsage:F0}%") } — {TempShort(snap.GpuTemp)}"
                );
                GpuTemp.Text = TempFull(snap.GpuTemp);

                // MEM
                SetGauge(
                    MemBar,
                    snap.MemUsage,
                    T.MemWarn,
                    T.MemAlert,
                    MemBadge,
                    MemBadgeHot,
                    $"Mémoire: { (double.IsNaN(snap.MemUsage) ? "--" : $"{snap.MemUsage:F0}%") }"
                );

                // DISK
                SetGauge(
                    DiskBar,
                    snap.DiskUsage,
                    T.DiskWarn,
                    T.DiskAlert,
                    DiskBadge,
                    DiskBadgeHot,
                    $"Disque: { (double.IsNaN(snap.DiskUsage) ? "--" : $"{snap.DiskUsage:F0}%") } — {TempShort(snap.DiskTemp)}"
                );
                DiskTemp.Text = TempFull(snap.DiskTemp);

                EvaluateAndReact(snap);
                StatusText.Text = $"Pulse @ {DateTime.Now:HH:mm:ss}";

                if (_monitoringBanterEnabled && DateTime.Now >= _nextPunch)
                {
                    Say(_punchlines.Random(), Mood.Playful);
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
            bar.Value = double.IsNaN(value) ? 0 : value;
            bar.ToolTip = tooltip;

            if (!double.IsNaN(value) && value >= alert)
            {
                bar.Foreground = _alert;
                badgeHot.Visibility = Visibility.Visible;
                badgeWarn.Visibility = Visibility.Collapsed;
            }
            else if (!double.IsNaN(value) && value >= warn)
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

        private void EvaluateAndReact(Snap snap)
        {
            bool alert = (!double.IsNaN(snap.CpuUsage) && snap.CpuUsage > T.CpuAlert)
                      || (!double.IsNaN(snap.GpuUsage) && snap.GpuUsage > T.GpuAlert)
                      || (!double.IsNaN(snap.MemUsage) && snap.MemUsage > T.MemAlert)
                      || (!double.IsNaN(snap.DiskUsage) && snap.DiskUsage > T.DiskAlert)
                      || (!double.IsNaN(snap.CpuTemp) && snap.CpuTemp > T.CpuTempAlert)
                      || (!double.IsNaN(snap.GpuTemp) && snap.GpuTemp > T.GpuTempAlert)
                      || (!double.IsNaN(snap.DiskTemp) && snap.DiskTemp > T.DiskTempAlert);

            bool warn = !alert && (
                        (!double.IsNaN(snap.CpuUsage) && snap.CpuUsage > T.CpuWarn) ||
                        (!double.IsNaN(snap.GpuUsage) && snap.GpuUsage > T.GpuWarn) ||
                        (!double.IsNaN(snap.MemUsage) && snap.MemUsage > T.MemWarn) ||
                        (!double.IsNaN(snap.DiskUsage) && snap.DiskUsage > T.DiskWarn) ||
                        (!double.IsNaN(snap.CpuTemp) && snap.CpuTemp > T.CpuTempWarn) ||
                        (!double.IsNaN(snap.GpuTemp) && snap.GpuTemp > T.GpuTempWarn) ||
                        (!double.IsNaN(snap.DiskTemp) && snap.DiskTemp > T.DiskTempWarn));

            if (alert) { SetAvatarMood("alert"); Say("🔥 Temp/charge élevée détectée !", Mood.Alert); }
            else if (warn) { SetAvatarMood("playful"); }
            else { SetAvatarMood("happy"); }
        }
    }
}
