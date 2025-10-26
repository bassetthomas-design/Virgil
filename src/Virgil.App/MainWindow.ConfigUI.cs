using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using Virgil.Core.Config;

namespace Virgil.App;

public partial class MainWindow : Window
{
    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Get();
        var txt = $"Seuils actuels :\n\nCPU {cfg.Thresholds.CpuWarn}/{cfg.Thresholds.CpuAlert}%\nGPU {cfg.Thresholds.GpuWarn}/{cfg.Thresholds.GpuAlert}%\nMEM {cfg.Thresholds.MemWarn}/{cfg.Thresholds.MemAlert}%\nDISK {cfg.Thresholds.DiskWarn}/{cfg.Thresholds.DiskAlert}%\nTemp CPU {cfg.Thresholds.CpuTempWarn}/{cfg.Thresholds.CpuTempAlert}°C\nTemp GPU {cfg.Thresholds.GpuTempWarn}/{cfg.Thresholds.GpuTempAlert}°C\nTemp DISK {cfg.Thresholds.DiskTempWarn}/{cfg.Thresholds.DiskTempAlert}°C\n\nFichier utilisateur : %AppData%\Virgil\user.json";
        Say(txt, Mood.Neutral);

        // Propose un modèle JSON pour user.json
        var model = new VirgilConfig{ Thresholds = cfg.Thresholds };
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions{ WriteIndented = true });
        Say("Modèle user.json :\n" + json, Mood.Neutral);
    }
}
