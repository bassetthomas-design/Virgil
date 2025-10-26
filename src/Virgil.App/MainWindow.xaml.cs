using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Virgil.Core.Services;

// Aliases (évite les ambiguïtés)
using Cleaning       = Virgil.Core.Services.CleaningService;
using Browsers       = Virgil.Core.Services.BrowserCleaningService;
using ExtendedClean  = Virgil.Core.Services.ExtendedCleaningService;
using AppsUpdate     = Virgil.Core.Services.ApplicationUpdateService;
using WinUpdate      = Virgil.Core.Services.WindowsUpdateService;
using DriverUpdate   = Virgil.Core.Services.DriverUpdateService;
using DefenderUpdate = Virgil.Core.Services.DefenderUpdateService;
using Presets        = Virgil.Core.Services.MaintenancePresetsService;

using ConfigSvc   = Virgil.Core.Config.ConfigService;
using Thresholds  = Virgil.Core.Config.Thresholds;

using AdvMon = Virgil.Core.Monitoring.AdvancedMonitoringService;

namespace Virgil.App;

public enum Mood { Neutral, Playful, Alert }

public sealed class ChatMessage : INotifyPropertyChanged
{
    private double _opacity = 1.0;
    private double _scale = 1.0;

    public string Text { get; set; } = string.Empty;
    public Brush BubbleBrush { get; set; } = Brushes.DimGray;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public double Opacity { get => _opacity; set { _opacity = value; OnPropertyChanged(); } }
    public double Scale   { get => _scale;   set { _scale   = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ChatMessage> _chat = new();

    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _survTimer  = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _thanosTimer= new() { Interval = TimeSpan.FromSeconds(0.5) }; // animation fine

    private DateTime _nextPunch = DateTime.Now.AddMinutes(1);

    private readonly AdvMon _monitor        = new();
    private readonly ConfigSvc _config      = new();
    private readonly Cleaning _cleaning     = new();
    private readonly Browsers _browsers     = new();
    private readonly ExtendedClean _extended= new();
    private readonly AppsUpdate _apps       = new();
    private readonly WinUpdate _wu          = new();
    private readonly DriverUpdate _drivers  = new();
    private readonly DefenderUpdate _def    = new();
    private readonly Presets _presets       = new();

    private Thresholds T => _config.Get().Thresholds;

    public MainWindow()
    {
        InitializeComponent();
        ChatList.ItemsSource = _chat;

        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();

        _survTimer.Tick += async (_, _) => await SurveillancePulseInternal();

        _thanosTimer.Tick += (_, _) => ThanosSweep();
        _thanosTimer.Start();

        Avatar?.SetMood("happy");
        Say("Salut, c’est Virgil. Active la surveillance pour commencer.", Mood.Neutral);
    }

    private void Say(string text, Mood mood)
    {
        Brush brush = mood switch
        {
            Mood.Alert   => new SolidColorBrush(Color.FromRgb(0xD9,0x3D,0x3D)),
            Mood.Playful => new SolidColorBrush(Color.FromRgb(0x9B,0x59,0xB6)),
            _            => new SolidColorBrush(Color.FromRgb(0x2C,0x3E,0x50)),
        };
        _chat.Add(new ChatMessage { Text = text, BubbleBrush = brush, CreatedUtc = DateTime.UtcNow, Opacity = 1.0, Scale = 1.0 });
    }

    private string GetRandomPunchline()
    {
        string[] lines =
        {
            "Je veille. Rien ne m’échappe.",
            "Hydrate-toi, pas que le CPU.",
            "Un petit nettoyage et ça repart.",
            "Winget est prêt à tout casser (dans le bon sens).",
        };
        var r = new Random();
        return lines[r.Next(lines.Length)];
    }

    private void PlanNextPunchline()
    {
        var r = new Random(); _nextPunch = DateTime.Now.AddMinutes(r.Next(1, 7));
    }

    private void SetAvatarMood(string mood)
    {
        try { Avatar?.SetMood(mood); } catch { }
    }

    private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
    {
        _survTimer.Start();
        StatusText.Text = "Surveillance: ON";
        Say("Surveillance démarrée.", Mood.Neutral);
        PlanNextPunchline();
    }

    private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _survTimer.Stop();
        StatusText.Text = "Surveillance: OFF";
        Say("Surveillance arrêtée.", Mood.Neutral);
    }

    private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Maintenance complète…", Mood.Neutral);
        try
        {
            var log = await _presets.FullAsync();
            Say(Summarize(log), Mood.Neutral);
            StatusText.Text = "Maintenance complète terminée";
        }
        catch (Exception ex)
        {
            Say("❌ Maintenance: " + ex.Message, Mood.Alert);
            StatusText.Text = "Erreur maintenance";
        }
        finally { ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private async void Action_CleanTemp(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Nettoyage TEMP…", Mood.Neutral);
        try
        {
            var log = await _cleaning.CleanTempAsync();
            Say(Summarize(log), Mood.Neutral);
            StatusText.Text = "Nettoyage TEMP terminé";
        }
        catch (Exception ex)
        {
            Say("❌ Nettoyage TEMP: " + ex.Message, Mood.Alert);
            StatusText.Text = "Erreur nettoyage TEMP";
        }
        finally { ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Nettoyage navigateurs…", Mood.Neutral);
        try
        {
            var report = await _browsers.AnalyzeAndCleanAsync(); 
            Say(Summarize(report), Mood.Neutral);
            StatusText.Text = "Nettoyage navigateurs terminé";
        }
        catch (Exception ex)
        {
            Say("❌ Navigateurs: " + ex.Message, Mood.Alert);
            StatusText.Text = "Erreur navigateurs";
        }
        finally { ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private async void Action_UpdateAll(object sender, RoutedEventArgs e)
    {
        ActionProgress.Visibility = Visibility.Visible;
        Say("Mises à jour globales…", Mood.Neutral);
        try
        {
            var a = await _apps.UpgradeAllAsync();
            var s = await _wu.StartScanAsync();
            var d = await _wu.StartDownloadAsync();
            var i = await _wu.StartInstallAsync();
            var r = await _drivers.UpgradeDriversAsync();
            var m = await _def.UpdateSignaturesAsync();
            var q = await _def.QuickScanAsync();

            var merged = string.Join("\n", new[] { a, s + d + i, r, m, q });
            Say(Summarize(merged), Mood.Neutral);
            StatusText.Text = "Mises à jour terminées";
        }
        catch (Exception ex)
        {
            Say("❌ Mises à jour: " + ex.Message, Mood.Alert);
            StatusText.Text = "Erreur mises à jour";
        }
        finally { ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private string Summarize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(OK)";
        var lines = text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0);
        return string.Join("\n", lines.Take(6)) + (lines.Count() > 6 ? "\n…" : "");
    }

    // Effet "Thanos": à partir de 60s, fade 1.0 -> 0 et scale 1.0 -> 0.8 sur 10s, puis suppression
    private void ThanosSweep()
    {
        var now = DateTime.UtcNow;
        for (int i = _chat.Count - 1; i >= 0; i--)
        {
            var msg = _chat[i];
            var age = (now - msg.CreatedUtc).TotalSeconds;
            if (age <= 60) continue;

            var t = (age - 60) / 10.0; // 0..1
            if (t >= 1.0) { _chat.RemoveAt(i); continue; }

            var k = Math.Clamp(t, 0, 1);
            msg.Opacity = 1.0 - k;
            msg.Scale = 1.0 - 0.2 * k;
        }
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Get();
        Say($"Seuils : CPU {cfg.Thresholds.CpuWarn}/{cfg.Thresholds.CpuAlert}% — GPU {cfg.Thresholds.GpuWarn}/{cfg.Thresholds.GpuAlert}% — MEM {cfg.Thresholds.MemWarn}/{cfg.Thresholds.MemAlert}%", Mood.Neutral);
        Say($"Temp CPU {cfg.Thresholds.CpuTempWarn}/{cfg.Thresholds.CpuTempAlert}°C — GPU {cfg.Thresholds.GpuTempWarn}/{cfg.Thresholds.GpuTempAlert}°C — DISK {cfg.Thresholds.DiskTempWarn}/{cfg.Thresholds.DiskTempAlert}°C", Mood.Neutral);
        Say("Édite %AppData%/Virgil/user.json pour override.", Mood.Neutral);
    }
}
