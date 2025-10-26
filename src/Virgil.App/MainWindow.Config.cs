using Virgil.Core.Config;
using Virgil.Core.Monitoring;
using System;
using System.Windows;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private readonly ConfigService _config = new();
    private Thresholds _t => _config.Get().Thresholds;

    private void EvaluateAndReact(HardwareSnapshot snap)
    {
        bool alert = snap.CpuUsage > _t.CpuAlert || snap.GpuUsage > _t.GpuAlert || snap.MemUsage > _t.MemAlert || snap.DiskUsage > _t.DiskAlert
                   || (!double.IsNaN(snap.CpuTemp) && snap.CpuTemp > _t.CpuTempAlert)
                   || (!double.IsNaN(snap.GpuTemp) && snap.GpuTemp > _t.GpuTempAlert)
                   || (!double.IsNaN(snap.DiskTemp) && snap.DiskTemp > _t.DiskTempAlert);
        bool warn  = !alert && (snap.CpuUsage > _t.CpuWarn || snap.GpuUsage > _t.GpuWarn || snap.MemUsage > _t.MemWarn || snap.DiskUsage > _t.DiskWarn
                   || (!double.IsNaN(snap.CpuTemp) && snap.CpuTemp > _t.CpuTempWarn)
                   || (!double.IsNaN(snap.GpuTemp) && snap.GpuTemp > _t.GpuTempWarn)
                   || (!double.IsNaN(snap.DiskTemp) && snap.DiskTemp > _t.DiskTempWarn));

        if (alert){ SetAvatarMood("alert"); Say("🔥 Temp/charge élevée détectée !", Mood.Alert); }
        else if (warn){ SetAvatarMood("playful"); }
        else { SetAvatarMood("happy"); }
    }

    private void SetAvatarMood(string mood)
    {
        try { Avatar?.SetMood(mood); } catch { }
    }
}
