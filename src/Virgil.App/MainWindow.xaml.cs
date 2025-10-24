#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using Virgil.Core.Services; // AdvancedMonitoringService, BrowserCleaningService, etc.

namespace Virgil.App
{
    // ======================= Chat message (fallback binding) =======================
    public class ChatMessage : INotifyPropertyChanged
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;

        private string _text = "";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

        private string _mood = "neutral";
        public string Mood { get => _mood; set { _mood = value; OnPropertyChanged(); UpdateBrush(); } }

        public System.Windows.Media.Brush BubbleBrush { get; private set; } =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

        private bool _isExpiring;
        public bool IsExpiring { get => _isExpiring; set { _isExpiring = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private void UpdateBrush()
        {
            BubbleBrush = Mood switch
            {
                "proud"    => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0x46, 0xFF, 0x7A)),
                "vigilant" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xE4, 0x6B)),
                "alert"    => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0x69, 0x61)),
                _          => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
            };
            OnPropertyChanged(nameof(BubbleBrush));
        }
    }

    // ======================= Fenêtre principale =======================
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

        private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _survTimer  = new() { Interval = TimeSpan.FromSeconds(10) };

        // Sondes
        private readonly UtilProbe _probe = new();
        private readonly AdvancedMonitoringService _adv = new();

        // Anti-répétitions & gouverneur de parole
        private string? _lastPulseLine;
        private DateTime _lastPulseAt = DateTime.MinValue;

        private DateTime _lastTalk = DateTime.MinValue;
        private readonly TimeSpan _minTalkGap = TimeSpan.FromSeconds(15);

        // Seuils d’alerte
        private float _cpuAlertC = 85, _gpuAlertC = 85;

        // Mode d’activité
        private enum SystemMode { Other, Internet, Game }
        private SystemMode _currentMode = SystemMode.Other;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Lire seuils depuis ConfigService si présent
            try
            {
                var cfg = new ConfigService();
                dynamic current = cfg.Current!;
                if (current != null)
                {
                    if (current.CpuTempAlert is float c) _cpuAlertC = c;
                    if (current.GpuTempAlert is float g) _gpuAlertC = g;
                }
            }
            catch { /* non bloquant */ }

            // Horloge
            _clockTimer.Tick += (_, __) =>
            {
                if (IsLoaded && ClockText != null)
                    ClockText.Text = DateTime.Now.ToString("dddd dd MMM HH:mm");
            };
            _clockTimer.Start();

            // Surveillance
            _survTimer.Tick += (_, __) => SurveillancePulse();

            // Avatar au neutre
            SetAvatarMood("neutral");

            // Message d’accueil
            Say(Dialogues.Startup(), "neutral");
        }

        // ========= Chat via ton VirgilChatPanel (ChatArea) =========
        private bool ShouldSpeak(string? category = null)
        {
            if (_currentMode == SystemMode.Game && category != "alert") return false;  // silencieux en jeu (sauf alertes)
            if (DateTime.UtcNow - _lastTalk < _minTalkGap) return false;               // anti-spam global
            return true;
        }
        private void Spoken() => _lastTalk = DateTime.UtcNow;

        private void Say(string text, string mood = "neutral", int ttlMs = 60000, string? category = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!ShouldSpeak(category)) return;

            // pour le binding si nécessaire (et pour export/trace)
            ChatMessages.Add(new ChatMessage { Text = text, Mood = mood, Timestamp = DateTime.Now });

            // contrôle custom (effets/TTL/autoscroll)
            try { ChatArea?.Post(text, mood, ttlMs); } catch { /* fallback silencieux */ }

            Spoken();
        }

        private void SetAvatarMood(string mood)
        {
            try
            {
                var vm = AvatarControl?.DataContext;
                vm?.GetType().GetMethod("SetMood")?.Invoke(vm, new object[] { mood });
            }
            catch { /* non bloquant */ }
        }

        // ========= Progression / statut =========
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

        // ========= Surveillance =========
        private void UpdateSurveillanceState()
        {
            OnPropertyChanged(nameof(SurveillanceButtonText));

            if (IsSurveillanceOn)
            {
                Say(Dialogues.SurveillanceStart(), "vigilant");
                _survTimer.Start();
                SurveillancePulse(); // tick immédiat
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
            // Mode d’activité courant
            _currentMode = DetectMode();

            // Punchline (anti-répétition)
            var line = _currentMode switch
            {
                SystemMode.Game     => "🎮 Je te laisse jouer tranquille. J’alerte si ça chauffe.",
                SystemMode.Internet => "🌐 Bonne navigation — je garde un œil sur les ressources.",
                _                   => Dialogues.PulseLineByTimeOfDay(),
            };

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
            GpuUsage = u.gpu;   // 0 si pas de sonde GPU
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

            // Alerte si seuil dépassé
            bool overCpu = snap.CpuTempC.HasValue && snap.CpuTempC.Value >= _cpuAlertC;
            bool overGpu = snap.GpuTempC.HasValue && snap.GpuTempC.Value >= _gpuAlertC;

            if (overCpu || overGpu)
            {
                var alert = Dialogues.AlertTemp();
                if (!string.Equals(alert, _lastPulseLine, StringComparison.OrdinalIgnoreCase))
                {
                    Say(alert, "alert", 15000, category: "alert");
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

        // ========= Actions (boutons) =========
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

                // Fallback winget (si dispo)
                await WingetUpgradeAllAsync();

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

                // Defender MAJ + scan rapide
                await DefenderUpdateAndQuickScanAsync();

                // Facultatif : réparation système
                // await RepairSystemFilesAsync();

                ProgressDone(Dialogues.Action("maintenance_full_done"));
                MarkFullMaintenanceDone();
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

                await WingetUpgradeAllAsync();

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

                await DefenderUpdateAndQuickScanAsync();

                ProgressDone(Dialogues.Action("update_all_done"));
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"{Dialogues.Action("error_prefix")} {ex.Message}", "alert");
            }
        }

        // ========= Maintenance auto (choisit Quick vs Full) =========
        private enum CleanProfile { Quick, Full }
        private CleanProfile ChooseCleanupProfile()
        {
            try
            {
                var sys = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
                var freeGb = sys.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                if (freeGb < 10) return CleanProfile.Full; // peu d’espace → full
            }
            catch { }

            try
            {
                var cfg = new ConfigService();
                DateTime? lastFull = cfg.Get<DateTime?>("Maintenance.LastFull");
                if (!lastFull.HasValue || (DateTime.UtcNow - lastFull.Value).TotalDays > 14)
                    return CleanProfile.Full; // si ça fait longtemps → full
            }
            catch { }

            return CleanProfile.Quick;
        }

        private void MarkFullMaintenanceDone()
        {
            try { var cfg = new ConfigService(); cfg.Set("Maintenance.LastFull", DateTime.UtcNow); cfg.Save(); } catch { }
        }

        private async void AutoMaintenanceButton_Click(object? s, RoutedEventArgs e)
        {
            var mode = ChooseCleanupProfile();
            if (mode == CleanProfile.Full) { await Dispatcher.InvokeAsync(() => FullMaintenanceButton_Click(s!, e)); }
            else                           { await Dispatcher.InvokeAsync(() => CleanButton_Click(s!, e)); }
        }

        // ========= Nettoyage TEMP avec progression =========
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

            // Dossiers (vider)
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

        // ========= winget upgrade fallback =========
        private static async Task WingetUpgradeAllAsync()
        {
            async Task<(int code, string outp)> Run(string file, string args)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file, Arguments = args, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                };
                using var p = new Process { StartInfo = psi };
                var sb = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                await Task.Run(() => p.WaitForExit());
                return (p.ExitCode, sb.ToString());
            }

            try { await Run("winget", "upgrade --all --include-unknown --accept-source-agreements --accept-package-agreements"); }
            catch { /* winget absent → on ignore */ }
        }

        // ========= Defender: MAJ + scan rapide =========
        private async Task DefenderUpdateAndQuickScanAsync()
        {
            string mp = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                               "Windows Defender", "MpCmdRun.exe");
            if (!File.Exists(mp)) { Say("Defender introuvable.", "alert"); return; }

            ProgressIndeterminate("Mise à jour Defender…");
            await RunProc(mp, "-SignatureUpdate");

            ProgressIndeterminate("Scan rapide Defender…");
            await RunProc(mp, "-Scan -ScanType 1");

            Say("Defender mis à jour + scan rapide terminé.", "proud");
            static async Task RunProc(string f, string a)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = f, Arguments = a, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                };
                using var p = new Process { StartInfo = psi };
                p.Start(); await Task.Run(() => p.WaitForExit());
            }
        }

        // ========= Outils Windows: DISM + SFC =========
        private async Task RepairSystemFilesAsync()
        {
            ProgressIndeterminate("DISM /RestoreHealth…");
            await Run("dism.exe", "/Online /Cleanup-Image /RestoreHealth");

            ProgressIndeterminate("SFC /scannow…");
            await Run("sfc.exe", "/scannow");

            Say("Réparation système terminée.", "proud");

            static async Task Run(string f, string a)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = f, Arguments = a, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                };
                using var p = new Process { StartInfo = psi };
                p.Start(); await Task.Run(() => p.WaitForExit());
            }
        }

        // ========= Détection mode d’activité =========
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        private SystemMode DetectMode()
        {
            try
            {
                var h = GetForegroundWindow();
                if (h != IntPtr.Zero && GetWindowThreadProcessId(h, out var pid) != 0 && pid != 0)
                {
                    var p = Process.GetProcessById((int)pid);
                    var n = (p.ProcessName ?? "").ToLowerInvariant();
                    if (n.Contains("chrome") || n.Contains("msedge") || n.Contains("firefox") || n.Contains("opera"))
                        return SystemMode.Internet;
                    if (n.Contains("steam") || n.Contains("epicgames") || n.Contains("battle.net") ||
                        n.Contains("fortnite") || n.Contains("eldenring") || n.Contains("cs2") || n.Contains("cod"))
                        return SystemMode.Game;
                }
            }
            catch { }
            return SystemMode.Other;
        }

        // ========= Sonde CPU/MEM/DISK robuste =========
        private sealed class UtilProbe : IDisposable
        {
            private PerformanceCounter? _cpu;
            private PerformanceCounter? _disk;
            private PerformanceCounter? _memAvail;
            private bool _warmedUp;

            public UtilProbe()
            {
                TryCreateCounters();
                try { _ = _cpu?.NextValue(); _ = _disk?.NextValue(); _ = _memAvail?.NextValue(); _warmedUp = true; }
                catch { _warmedUp = false; }
            }

            private void TryCreateCounters()
            {
                _cpu  = TryOne(new[] { ("Processor", "% Processor Time", "_Total"),
                                       ("Processor Information", "% Processor Time", "_Total") });
                _disk = TryOne(new[] { ("PhysicalDisk", "% Disk Time", "_Total"),
                                       ("LogicalDisk", "% Disk Time", "_Total") });
                try { _memAvail = new PerformanceCounter("Memory", "Available MBytes"); } catch { _memAvail = null; }
            }
            private static PerformanceCounter? TryOne((string cat, string ctr, string inst)[] opts)
            {
                foreach (var (c, t, i) in opts)
                    try { if (PerformanceCounterCategory.Exists(c)) return new(c, t, i, true); } catch { }
                return null;
            }

            public (double cpu, double gpu, double mem, double disk) Read()
            {
                if (!_warmedUp)
                {
                    try { _ = _cpu?.NextValue(); _ = _disk?.NextValue(); _ = _memAvail?.NextValue(); _warmedUp = true; }
                    catch { }
                }

                double cpu = Safe(_cpu), disk = Safe(_disk);
                double memUsed;
                try
                {
                    var totalMb = TotalMb();
                    var freeMb = Safe(_memAvail);
                    memUsed = totalMb > 0 ? (1.0 - freeMb / totalMb) * 100.0 : 0;
                }
                catch { memUsed = 0; }

                double gpu = 0; // ajoute ta sonde GPU si besoin
                return (Clamp(cpu), Clamp(gpu), Clamp(memUsed), Clamp(disk));
            }
            private static double Safe(PerformanceCounter? c) { try { return c?.NextValue() ?? 0; } catch { return 0; } }
            private static double TotalMb() { try { var ci = new Microsoft.VisualBasic.Devices.ComputerInfo(); return ci.TotalPhysicalMemory / 1024.0 / 1024.0; } catch { return 0; } }
            private static double Clamp(double v) => Math.Max(0, Math.Min(100, double.IsFinite(v) ? v : 0));
            public void Dispose() { try { _cpu?.Dispose(); } catch { } try { _disk?.Dispose(); } catch { } try { _memAvail?.Dispose(); } catch { } }
        }
    }

    // ======================= Dialogues (JSON facultatif) =======================
    internal static class Dialogues
    {
        private static readonly Random R = new();
        private static System.Text.Json.Nodes.JsonObject? _data;

        static Dialogues()
        {
            try
            {
                var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "virgil-dialogues.json");
                if (File.Exists(file))
                {
                    _data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(File.ReadAllText(file));
                }
            }
            catch { /* ignore */ }
        }

        private static string Pick(string section, string fallback = "…")
        {
            try
            {
                if (_data != null && _data.TryGetPropertyValue(section, out var node) && node is System.Text.Json.Nodes.JsonArray arr)
                {
                    var list = arr.Select(x => x?.ToString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    if (list.Count > 0) return list[R.Next(list.Count)];
                }
            }
            catch { }
            return fallback;
        }

        public static string Startup()            => Pick("startup", "Virgil en place. Système prêt.");
        public static string SurveillanceStart()  => Pick("surveillance_start", "Surveillance activée. Je garde un œil 👀");
        public static string SurveillanceStop()   => Pick("surveillance_stop", "Surveillance arrêtée.");
        public static string PulseLineByTimeOfDay()
        {
            var h = DateTime.Now.Hour;
            if (h is >= 6 and < 12) return Pick("time_morning", "☀️ Bonjour ! Tout roule.");
            if (h is >= 12 and < 18) return Pick("time_afternoon", "🛡️ Je surveille pendant que tu bosses.");
            if (h is >= 18 and < 23) return Pick("time_evening", "🌇 Fin de journée ? Je garde l’œil.");
            return Pick("time_night", "🌙 Nuit calme, je veille.");
        }
        public static string AlertTemp()          => Pick("alert_temp", "🔥 Ça chauffe. Évite les charges lourdes et ventile.");
        public static string Action(string key)   => Pick($"action_{key}", key);
    }
}
