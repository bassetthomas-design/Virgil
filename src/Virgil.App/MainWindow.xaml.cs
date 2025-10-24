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

// WPF media (alias pour éviter toute ambiguïté avec System.Drawing)
using Media = System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

using Virgil.Core.Services; // AdvancedMonitoringService, ConfigService, BrowserCleaningService, etc.

namespace Virgil.App
{
    // ============ Modèle de message (utile pour le binding & fallback visuel) ============
    public class ChatMessage : INotifyPropertyChanged
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;

        private string _text = "";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

        private string _mood = "neutral";
        public string Mood
        {
            get => _mood;
            set { _mood = value; OnPropertyChanged(); UpdateBrush(); }
        }

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

    // =================================== Fenêtre ===================================
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

        // Stats bindées au panneau
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

        // Anti-répétitions de punchlines
        private string? _lastPulseLine;
        private DateTime _lastPulseAt = DateTime.MinValue;

        // Seuils d’alerte (peuvent être surchargés via config)
        private float _cpuAlertC = 85f, _gpuAlertC = 85f;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Charger seuils depuis la conf si disponibles (gère float? -> float)
            try
            {
                var cur = _config.Current;
                if (cur != null)
                {
                    float? c = cur.CpuTempAlert;
                    float? g = cur.GpuTempAlert;

                    if (c.HasValue) _cpuAlertC = c.Value;
                    if (g.HasValue) _gpuAlertC = g.Value;
                }
            }
            catch
            {
                // Non bloquant si la config n'est pas lisible
            }

            // Horloge
            _clockTimer.Tick += (_, __) =>
            {
                if (IsLoaded && ClockText != null)
                    ClockText.Text = DateTime.Now.ToString("dddd dd MMM HH:mm");
            };
            _clockTimer.Start();

            // Surveillance
            _survTimer.Tick += (_, __) => SurveillancePulse();

            // Avatar neutre au démarrage
            SetAvatarMood("neutral");

            // Message d’accueil (via Dialogues.cs)
            try { Say(Dialogues.Startup(), "neutral"); } catch { Say("Virgil en place. Système prêt.", "neutral"); }
        }

        // ================== Chat (via ton VirgilChatPanel) ==================
        private void Say(string text, string mood = "neutral", int ttlMs = 60000)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Ajout au binding (utile si tu affiches aussi ChatMessages)
            ChatMessages.Add(new ChatMessage { Text = text, Mood = mood, Timestamp = DateTime.Now });

            // Envoi au panneau custom (effet, TTL, autoscroll)
            try { ChatArea?.Post(text, mood, ttlMs); } catch { /* fallback silencieux */ }
        }

        private void SetAvatarMood(string mood)
        {
            try
            {
                var vm = AvatarControl?.DataContext; // si tu as VirgilAvatarViewModel bindé en XAML
                vm?.GetType().GetMethod("SetMood")?.Invoke(vm, new object[] { mood });
            }
            catch
            {
                // non bloquant
            }
        }

        // ================== Progression / Statut ==================
        private void Progress(double percent, string status, string mood = "vigilant")
        {
            percent = Math.Max(0, Math.Min(100, percent));
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
                try { Say(Dialogues.SurveillanceStart(), "vigilant"); } catch { Say("Surveillance activée. Je garde un œil 👀", "vigilant"); }
                _survTimer.Start();
                SurveillancePulse(); // premier tick immédiat
            }
            else
            {
                _survTimer.Stop();
                try { Say(Dialogues.SurveillanceStop(), "neutral"); } catch { Say("Surveillance arrêtée.", "neutral"); }
                SetAvatarMood("neutral");
            }
        }

        private void SurveillancePulse()
        {
            // Punchline (anti-répétition 2 min)
            string line;
            try { line = Dialogues.PulseLineByTimeOfDay(); }
            catch
            {
                var h = DateTime.Now.Hour;
                line = h switch
                {
                    >= 6 and < 12  => "☀️ Bonjour ! Tout roule.",
                    >= 12 and < 18 => "🛡️ Je surveille pendant que tu bosses.",
                    >= 18 and < 23 => "🌇 Fin de journée ? Je garde l’œil.",
                    _              => "🌙 Nuit calme, je veille.",
                };
            }

            if (!string.Equals(line, _lastPulseLine, StringComparison.OrdinalIgnoreCase) ||
                (DateTime.UtcNow - _lastPulseAt) > TimeSpan.FromMinutes(2))
            {
                Say(line, "vigilant", 15000);
                _lastPulseLine = line;
                _lastPulseAt = DateTime.UtcNow;
            }

            // Usages
            var u = _probe.Read();
            CpuUsage = u.cpu;
            MemUsage = u.mem;
            GpuUsage = u.gpu;
            DiskUsage = u.disk;

            // Températures
            HardwareSnapshot snap;
            try { snap = _adv.Read(); }
            catch { snap = new HardwareSnapshot { CpuTempC = null, GpuTempC = null, DiskTempC = null }; }

            CpuTempText = snap.CpuTempC.HasValue ? $"CPU: {snap.CpuTempC.Value:F0} °C" : "CPU: —";
            GpuTempText = snap.GpuTempC.HasValue ? $"GPU: {snap.GpuTempC.Value:F0} °C" : "GPU: —";
            DiskTempText = snap.DiskTempC.HasValue ? $"Disque: {snap.DiskTempC.Value:F0} °C" : "Disque: —";
            OnPropertyChanged(nameof(CpuTempText));
            OnPropertyChanged(nameof(GpuTempText));
            OnPropertyChanged(nameof(DiskTempText));

            // Alerte avatar + phrase si seuil dépassé
            bool overCpu = snap.CpuTempC.HasValue && snap.CpuTempC.Value >= _cpuAlertC;
            bool overGpu = snap.GpuTempC.HasValue && snap.GpuTempC.Value >= _gpuAlertC;

            if (overCpu || overGpu)
            {
                string alert;
                try { alert = Dialogues.AlertTemp(); } catch { alert = "🔥 Ça chauffe un peu. Pense à ventiler."; }

                if (!string.Equals(alert, _lastPulseLine, StringComparison.OrdinalIgnoreCase))
                {
                    Say(alert, "alert");
                    _lastPulseLine = alert;
                    _lastPulseAt = DateTime.UtcNow;
                }
                SetAvatarMood("alert");
            }
            else
            {
                if (IsSurveillanceOn) SetAvatarMood("vigilant");
            }
        }

        // ================== Actions (boutons) ==================
        private async void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, Dialogues.Action("maintenance_full_start"));

                Progress(10, Dialogues.Action("clean_temp_start"));
                await Task.Run(CleanTempWithProgressInternal);

                Progress(30, Dialogues.Action("clean_browsers_start"));
                var browsers = new BrowserCleaningService();
                var bRep = await Task.Run(() => browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                Say(Dialogues.Action("clean_browsers_done") + $" (~{bRep.BytesDeleted / (1024.0 * 1024):F1} MB)");

                Progress(50, Dialogues.Action("clean_extended_start"));
                var ext = new ExtendedCleaningService();
                var exRep = await Task.Run(() => ext.AnalyzeAndClean());
                Say(Dialogues.Action("clean_extended_done") + $" (~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB)");

                ProgressIndeterminate(Dialogues.Action("update_apps_games_start"));
                var app = new ApplicationUpdateService();
                var txt = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
                if (!string.IsNullOrWhiteSpace(txt)) Say(Dialogues.Action("update_apps_done"));

                var games = new GameUpdateService();
                var gOut = await games.UpdateAllAsync();
                if (!string.IsNullOrWhiteSpace(gOut)) Say(gOut);

                ProgressIndeterminate(Dialogues.Action("update_windows_start"));
                var wu = new WindowsUpdateService();
                await wu.StartScanAsync();
                await wu.StartDownloadAsync();
                await wu.StartInstallAsync();
                Say(Dialogues.Action("update_windows_done"));

                ProgressIndeterminate(Dialogues.Action("update_drivers_start"));
                var drv = new DriverUpdateService();
                var dOut = await drv.UpgradeDriversAsync();
                if (!string.IsNullOrWhiteSpace(dOut)) Say(Dialogues.Action("update_drivers_done"));

                ProgressDone(Dialogues.Action("maintenance_full_done"));
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"{Dialogues.Action("error_prefix")} {ex.Message}", "alert");
            }
        }

        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, Dialogues.Action("clean_temp_start"));
                await Task.Run(CleanTempWithProgressInternal);
                ProgressDone(Dialogues.Action("clean_temp_done"));
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"{Dialogues.Action("error_prefix")} {ex.Message}", "alert");
            }
        }

        private async void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, Dialogues.Action("clean_browsers_start"));
                var svc = new BrowserCleaningService();
                var rep = await Task.Run(() => svc.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                ProgressDone(Dialogues.Action("clean_browsers_done") + $" (~{rep.BytesDeleted / (1024.0 * 1024):F1} MB)");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"{Dialogues.Action("error_prefix")} {ex.Message}", "alert");
            }
        }

        private async void UpdateAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate(Dialogues.Action("update_apps_games_start"));

                var app = new ApplicationUpdateService();
                var txt = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
                if (!string.IsNullOrWhiteSpace(txt)) Say(Dialogues.Action("update_apps_done"));

                var games = new GameUpdateService();
                var gOut = await games.UpdateAllAsync();
                if (!string.IsNullOrWhiteSpace(gOut)) Say(gOut);

                var drv = new DriverUpdateService();
                var dOut = await drv.UpgradeDriversAsync();
                if (!string.IsNullOrWhiteSpace(dOut)) Say(Dialogues.Action("update_drivers_done"));

                var wu = new WindowsUpdateService();
                await wu.StartScanAsync();
                await wu.StartDownloadAsync();
                await wu.StartInstallAsync();
                Say(Dialogues.Action("update_windows_done"));

                ProgressDone(Dialogues.Action("update_all_done"));
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"{Dialogues.Action("error_prefix")} {ex.Message}", "alert");
            }
        }

        // ================== Nettoyage TEMP avec progression ==================
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
                catch { /* verrouillé, ignore */ }

                done++;
                var p = Math.Floor(done / total * 100);
                Dispatcher.Invoke(() => Progress(p, $"Nettoyage TEMP… {p:0}%"));
            }

            // Dossiers (vider en profondeur)
            foreach (var t in targets)
            {
                try
                {
                    foreach (var d in Directory.EnumerateDirectories(t, "*", SearchOption.AllDirectories)
                                               .OrderByDescending(s => s.Length))
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }
                }
                catch { }
            }

            Dispatcher.Invoke(() =>
                Say($"TEMP analysé ~{bytesFound / (1024.0 * 1024):F1} MB — supprimé ~{bytesDeleted / (1024.0 * 1024):F1} MB", "proud"));
        }

        // ================== Sonde simple CPU/MEM/DISK (GPU=0 par défaut) ==================
        private sealed class UtilProbe : IDisposable
        {
            private readonly PerformanceCounter? _cpu;
            private readonly PerformanceCounter? _disk;
            private readonly PerformanceCounter? _memAvail;

            public UtilProbe()
            {
                try { _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true); } catch { _cpu = null; }
                try { _disk = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", true); } catch { _disk = null; }
                try { _memAvail = new PerformanceCounter("Memory", "Available MBytes"); } catch { _memAvail = null; }
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
                    memUsed = (totalMb > 0) ? (1.0 - (freeMb / totalMb)) * 100.0 : 0;
                }
                catch { memUsed = 0; }

                double gpu = 0; // à 0 par défaut (pas de compteur standard)

                return (Clamp(cpu), Clamp(gpu), Clamp(memUsed), Clamp(disk));
            }

            private static double SafeRead(PerformanceCounter? c)
            {
                try { return c?.NextValue() ?? 0; } catch { return 0; }
            }

            private static double GetTotalMemoryMB()
            {
                try
                {
                    var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
                    return ci.TotalPhysicalMemory / 1024.0 / 1024.0;
                }
                catch { return 0; }
            }

            private static double Clamp(double v) => Math.Max(0, Math.Min(100, double.IsFinite(v) ? v : 0));

            public void Dispose()
            {
                try { _cpu?.Dispose(); } catch { }
                try { _disk?.Dispose(); } catch { }
                try { _memAvail?.Dispose(); } catch { }
            }
        }
    }
}
