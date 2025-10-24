#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

// Alias WPF (évite le conflit avec System.Drawing)
using Media = System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

using Virgil.Core.Services; // AdvancedMonitoringService, ConfigService, BrowserCleaningService, etc.

namespace Virgil.App
{
    // ===== Modèle message =====
    public class ChatMessage : INotifyPropertyChanged
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;

        private string _text = "";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

        private string _mood = "neutral";
        public string Mood { get => _mood; set { _mood = value; OnPropertyChanged(); UpdateBrush(); } }

        public MediaBrush BubbleBrush { get; private set; } =
            new SolidColorBrush(MediaColor.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

        private bool _isExpiring;
        public bool IsExpiring { get => _isExpiring; set { _isExpiring = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private void UpdateBrush()
        {
            BubbleBrush = Mood switch
            {
                "proud"    => new SolidColorBrush(MediaColor.FromArgb(0x22, 0x46, 0xFF, 0x7A)),
                "vigilant" => new SolidColorBrush(MediaColor.FromArgb(0x22, 0xFF, 0xE4, 0x6B)),
                "alert"    => new SolidColorBrush(MediaColor.FromArgb(0x22, 0xFF, 0x69, 0x61)),
                _          => new SolidColorBrush(MediaColor.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
            };
            OnPropertyChanged(nameof(BubbleBrush));
        }
    }

    // ===================== FENÊTRE PRINCIPALE =====================
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // === Bindings UI ===
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

        private bool _isMonitoring;
        public bool IsSurveillanceOn
        {
            get => _isMonitoring;
            set { _isMonitoring = value; OnPropertyChanged(); UpdateSurveillanceState(); }
        }

        public string SurveillanceButtonText => IsSurveillanceOn ? "Arrêter la surveillance" : "Démarrer la surveillance";

        // Stats
        private double _cpu, _gpu, _mem, _disk;
        public double CpuUsage { get => _cpu; set { _cpu = value; OnPropertyChanged(); } }
        public double GpuUsage { get => _gpu; set { _gpu = value; OnPropertyChanged(); } }
        public double MemUsage { get => _mem; set { _mem = value; OnPropertyChanged(); } }
        public double DiskUsage { get => _disk; set { _disk = value; OnPropertyChanged(); } }

        public string CpuTempText { get; set; } = "CPU: —";
        public string GpuTempText { get; set; } = "GPU: —";
        public string DiskTempText { get; set; } = "Disque: —";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        // Timers
        private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _survTimer  = new() { Interval = TimeSpan.FromSeconds(10) };

        // Sondes & services
        private readonly UtilProbe _probe = new();
        private readonly AdvancedMonitoringService _adv = new();
        private readonly ConfigService _config = new();

        // Anti-répétitions
        private string? _lastPulseLine;
        private DateTime _lastPulseAt = DateTime.MinValue;

        // Seuils alertes
        private float _cpuAlertC = 85f, _gpuAlertC = 85f;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Charger seuils depuis config
            try
            {
                var cur = _config.Current;
                if (cur != null)
                {
                    if (cur.CpuTempAlert is float c) _cpuAlertC = c;
                    if (cur.GpuTempAlert is float g) _gpuAlertC = g;
                }
            }
            catch { }

            // Horloge
            _clockTimer.Tick += (_, __) =>
            {
                if (IsLoaded && ClockText != null)
                    ClockText.Text = DateTime.Now.ToString("dddd dd MMM HH:mm");
            };
            _clockTimer.Start();

            // Surveillance
            _survTimer.Tick += (_, __) => SurveillancePulse();

            // Avatar neutre
            SetAvatarMood("neutral");

            // Message d’accueil
            try { Say(Dialogues.Startup(), "neutral"); }
            catch { Say("Virgil en place. Système prêt.", "neutral"); }
        }

        // ================== Chat ==================
        private void Say(string text, string mood = "neutral", int ttlMs = 60000)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            ChatMessages.Add(new ChatMessage { Text = text, Mood = mood, Timestamp = DateTime.Now });
            try { ChatArea?.Post(text, mood, ttlMs); } catch { }
        }

        // ✅ intégré ici : SetAvatarMood()
        private void SetAvatarMood(string mood)
        {
            try { AvatarControl?.SetMood(mood); }
            catch { /* non bloquant */ }
        }

        // ================== Progression / Statut ==================
        private void Progress(double percent, string status, string mood = "vigilant")
        {
            percent = Math.Clamp(percent, 0, 100);
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = percent;
            StatusText.Text = status;
            Say(status, mood);
            SetAvatarMood(percent >= 100 ? "proud" : mood);
        }

        private void ProgressIndeterminate(string status, string mood = "vigilant")
        {
            TaskProgress.IsIndeterminate = true;
            StatusText.Text = status;
            Say(status, mood);
            SetAvatarMood(mood);
        }

        private void ProgressDone(string status = "Terminé.")
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 100;
            StatusText.Text = status;
            Say(status, "proud");
            SetAvatarMood("proud");
        }

        private void ProgressReset()
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 0;
            StatusText.Text = "Prêt.";
            SetAvatarMood("neutral");
        }

        // ================== Surveillance ==================
        private void UpdateSurveillanceState()
        {
            OnPropertyChanged(nameof(SurveillanceButtonText));

            if (IsSurveillanceOn)
            {
                Say(Dialogues.SurveillanceStart(), "vigilant");
                _survTimer.Start();
                SurveillancePulse();
            }
            else
            {
                _survTimer.Stop();
                Say(Dialogues.SurveillanceStop(), "neutral");
                SetAvatarMood("neutral");
            }
        }

        private void SurveillancePulse()
        {
            var line = Dialogues.PulseLineByTimeOfDay();
            if (!string.Equals(line, _lastPulseLine, StringComparison.OrdinalIgnoreCase) ||
                (DateTime.UtcNow - _lastPulseAt) > TimeSpan.FromMinutes(2))
            {
                Say(line, "vigilant", 15000);
                _lastPulseLine = line;
                _lastPulseAt = DateTime.UtcNow;
            }

            var u = _probe.Read();
            CpuUsage = u.cpu;
            MemUsage = u.mem;
            GpuUsage = u.gpu;
            DiskUsage = u.disk;

            HardwareSnapshot snap;
            try { snap = _adv.Read(); }
            catch { snap = new HardwareSnapshot(); }

            CpuTempText = snap.CpuTempC.HasValue ? $"CPU: {snap.CpuTempC.Value:F0} °C" : "CPU: —";
            GpuTempText = snap.GpuTempC.HasValue ? $"GPU: {snap.GpuTempC.Value:F0} °C" : "GPU: —";
            DiskTempText = snap.DiskTempC.HasValue ? $"Disque: {snap.DiskTempC.Value:F0} °C" : "Disque: —";
            OnPropertyChanged(nameof(CpuTempText));
            OnPropertyChanged(nameof(GpuTempText));
            OnPropertyChanged(nameof(DiskTempText));

            bool overCpu = snap.CpuTempC >= _cpuAlertC;
            bool overGpu = snap.GpuTempC >= _gpuAlertC;

            if (overCpu || overGpu)
            {
                Say(Dialogues.AlertTemp(), "alert");
                SetAvatarMood("alert");
            }
            else if (IsSurveillanceOn)
            {
                SetAvatarMood("vigilant");
            }
        }

        // ================== Nettoyage TEMP ==================
        private void CleanTempWithProgressInternal()
        {
            var targets = new[]
            {
                Environment.ExpandEnvironmentVariables("%TEMP%"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            }.Where(Directory.Exists).ToList();

            var files = targets.SelectMany(t =>
            {
                try { return Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories); }
                catch { return Enumerable.Empty<string>(); }
            }).ToList();

            double total = files.Count == 0 ? 1 : files.Count;
            double done = 0;
            long bytesFound = 0, bytesDeleted = 0;

            foreach (var f in files)
                try { bytesFound += new FileInfo(f).Length; } catch { }

            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    var len = fi.Exists ? fi.Length : 0;
                    File.SetAttributes(f, FileAttributes.Normal);
                    fi.Delete();
                    bytesDeleted += len;
                }
                catch { }

                done++;
                var p = Math.Floor(done / total * 100);
                Dispatcher.Invoke(() => Progress(p, $"Nettoyage TEMP… {p:0}%"));
            }

            Dispatcher.Invoke(() =>
                Say($"TEMP analysé ~{bytesFound / (1024.0 * 1024):F1} MB — supprimé ~{bytesDeleted / (1024.0 * 1024):F1} MB", "proud"));
        }

        // ================== Sondes ==================
        private sealed class UtilProbe : IDisposable
        {
            private readonly PerformanceCounter? _cpu;
            private readonly PerformanceCounter? _disk;
            private readonly PerformanceCounter? _memAvail;

            public UtilProbe()
            {
                try { _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true); } catch { }
                try { _disk = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true); } catch { }
                try { _memAvail = new PerformanceCounter("Memory", "Available MBytes"); } catch { }
            }

            public (double cpu, double gpu, double mem, double disk) Read()
            {
                double cpu = SafeRead(_cpu);
                double disk = SafeRead(_disk);
                double memUsed;

                try
                {
                    var totalMb = GetTotalMemoryMB();
                    var freeMb = SafeRead(_memAvail);
                    memUsed = (1.0 - (freeMb / totalMb)) * 100.0;
                }
                catch { memUsed = 0; }

                return (Clamp(cpu), 0, Clamp(memUsed), Clamp(disk));
            }

            private static double SafeRead(PerformanceCounter? c)
                => c != null ? c.NextValue() : 0;

            private static double GetTotalMemoryMB()
            {
                try
                {
                    var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
                    return ci.TotalPhysicalMemory / 1024.0 / 1024.0;
                }
                catch { return 0; }
            }

            private static double Clamp(double v)
                => Math.Max(0, Math.Min(100, double.IsFinite(v) ? v : 0));

            public void Dispose()
            {
                _cpu?.Dispose();
                _disk?.Dispose();
                _memAvail?.Dispose();
            }
        }
    }
}
