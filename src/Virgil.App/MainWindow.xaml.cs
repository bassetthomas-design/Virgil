using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Virgil.Core.Config;
using Virgil.Core.Monitoring;
using Virgil.Core.Services;

namespace Virgil.App;

public enum Mood { Neutral, Playful, Alert }

public sealed class ChatMessage
{
    public string Text { get; set; } = string.Empty;
    public Brush BubbleBrush { get; set; } = Brushes.DimGray;
}

public partial class MainWindow : Window
{
    // UI data
    private readonly ObservableCollection<ChatMessage> _chat = new();

    // Timers
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _survTimer  = new() { Interval = TimeSpan.FromSeconds(2) };
    private DateTime _nextPunch = DateTime.Now.AddMinutes(1);

    // Services
    private readonly AdvancedMonitoringService _monitor = new();
    private readonly ConfigService _config = new();
    private readonly CleaningService _cleaning = new();
    private readonly BrowserCleaningService _browsers = new();
    private readonly ExtendedCleaningService _extended = new();
    private readonly ApplicationUpdateService _apps = new();
    private readonly WindowsUpdateService _wu = new();
    private readonly DriverUpdateService _drivers = new();
    private readonly DefenderUpdateService _def = new();
    private readonly MaintenancePresetsService _presets = new();

    private Thresholds T => _config.Get().Thresholds;

    public MainWindow()
    {
        InitializeComponent();
        ChatList.ItemsSource = _chat;

        // clock
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();

        // surveillance
        _survTimer.Tick += async (_, _) => await SurveillancePulseInternal();

        // greeting
        Say("Hello, c'est Virgil. Clique sur ‘Démarrer la surveillance’ pour commencer !", Mood.Neutral);
    }

    // ===== Chat helpers =====
    private void Say(string text, Mood mood)
    {
        Brush brush = mood switch
        {
            Mood.Alert   => new SolidColorBrush(Color.FromRgb(0xD9,0x3D,0x3D)),
            Mood.Playful => new SolidColorBrush(Color.FromRgb(0x9B,0x59,0xB6)),
            _            => new SolidColorBrush(Color.FromRgb(0x44,0x55,0x66)),
        };
        _chat.Add(new ChatMessage { Text = text, BubbleBrush = brush });
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
        var r = new Random(); return lines[r.Next(lines.Length)];
    }

    private void PlanNextPunchline()
    {
        var r = new Random(); _nextPunch = DateTime.Now.AddMinutes(r.Next(1, 7));
    }

    private void SetAvatarMood(string mood)
    {
        try { Avatar?.SetMood(mood); } catch { }
    }

    // ===== Events from XAML =====
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
        Say("Maintenance complète lancée…", Mood.Neutral);
        try
        {
            var log = await _presets.FullAsync();
            Say(log, Mood.Neutral);
            StatusText.Text = "Maintenance complète terminée";
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
            Say(log, Mood.Neutral);
            StatusText.Text = "Nettoyage TEMP terminé";
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
            Say(report, Mood.Neutral);
            StatusText.Text = "Nettoyage navigateurs terminé";
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
            Say(a + "\n" + s + d + i + "\n" + r + "\n" + m + "\n" + q, Mood.Neutral);
            StatusText.Text = "Mises à jour globales terminées";
        }
        finally { ActionProgress.Visibility = Visibility.Collapsed; }
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Get();
        Say($"Seuils : CPU {cfg.Thresholds.CpuWarn}/{cfg.Thresholds.CpuAlert}% — GPU {cfg.Thresholds.GpuWarn}/{cfg.Thresholds.GpuAlert}% — MEM {cfg.Thresholds.MemWarn}/{cfg.Thresholds.MemAlert}%", Mood.Neutral);
        Say($"Temp CPU {cfg.Thresholds.CpuTempWarn}/{cfg.Thresholds.CpuTempAlert}°C — GPU {cfg.Thresholds.GpuTempWarn}/{cfg.Thresholds.GpuTempAlert}°C — DISK {cfg.Thresholds.DiskTempWarn}/{cfg.Thresholds.DiskTempAlert}°C", Mood.Neutral);
        Say("Édite %AppData%/Virgil/user.json pour override.", Mood.Neutral);
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var txt = UserInput.Text?.Trim();
        if (!string.IsNullOrEmpty(txt))
        {
            Say(txt, Mood.Playful);
            UserInput.Clear();
        }
    }

    // ===== Monitoring pulse =====
    private async System.Threading.Tasks.Task SurveillancePulseInternal()
    {
        try
        {
            var snap = await _monitor.GetSnapshotAsync();
            StatusText.Text = $"Pulse @ {DateTime.Now:HH:mm:ss}";

            EvaluateAndReact(snap);
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

    private void EvaluateAndReact(HardwareSnapshot snap)
    {
        bool alert = snap.CpuUsage > T.CpuAlert || snap.GpuUsage > T.GpuAlert || snap.MemUsage > T.MemAlert || snap.DiskUsage > T.DiskAlert
                   || (!double.IsNaN(snap.CpuTemp) && snap.CpuTemp > T.CpuTempAlert)
                   || (!double.IsNaN(snap.GpuTemp) && snap.GpuTemp > T.GpuTempAlert)
                   || (!double.IsNaN(snap.DiskTemp) && snap.DiskTemp > T.DiskTempAlert);
        bool warn  = !alert && (snap.CpuUsage > T.CpuWarn || snap.GpuUsage > T.GpuWarn || snap.MemUsage > T.MemWarn || snap.DiskUsage > T.DiskWarn
                   || (!double.IsNaN(snap.CpuTemp) && snap.CpuTemp > T.CpuTempWarn)
                   || (!double.IsNaN(snap.GpuTemp) && snap.GpuTemp > T.GpuTempWarn)
                   || (!double.IsNaN(snap.DiskTemp) && snap.DiskTemp > T.DiskTempWarn));

        if (alert) { SetAvatarMood("alert"); Say("🔥 Temp/charge élevée détectée !", Mood.Alert); }
        else if (warn) { SetAvatarMood("playful"); }
        else { SetAvatarMood("happy"); }
    }
}
