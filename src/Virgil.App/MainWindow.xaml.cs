#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using Serilog.Events;
using Virgil.Core;
using Virgil.Core.Services;
using CoreServices = Virgil.Core.Services;

namespace Virgil.App
{
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
        private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

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

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // === Bindings UI ===
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
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
        private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private readonly CoreServices.ConfigService _config;
        private readonly MonitoringService? _monitoringService;
        private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _surveillanceTimer = new() { Interval = TimeSpan.FromSeconds(10) };

        private bool _isMonitoring;

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();

            _config = new CoreServices.ConfigService();
            CoreServices.LoggingService.Init(LogEventLevel.Information);

            // Avatar VM (si dispo)
            try
            {
                var vmType = Type.GetType("Virgil.App.Controls.VirgilAvatarViewModel, Virgil.App");
                if (vmType != null && AvatarControl != null)
                {
                    var vm = Activator.CreateInstance(vmType);
                    AvatarControl.DataContext = vm;
                    SetAvatarMood("neutral");
                }
            }
            catch { /* ignore */ }

            // Monitoring service (mesures bas niveau)
            try
            {
                _monitoringService = new MonitoringService();
            }
            catch { _monitoringService = null; }

            // Horloge en barre d’état
            _clockTimer.Tick += (_, __) => { ClockText.Text = DateTime.Now.ToString("dddd dd MMM HH:mm"); };
            _clockTimer.Start();

            // Surveillance (chat + stats) — seulement si activé
            _surveillanceTimer.Tick += (_, __) => SurveillancePulse();

            // Message d’accueil
            Say(Dialogues.Startup(), "neutral");
        }

        // ================== CHAT (messages + disparition 60s) ==================
        private void Say(string text, string mood = "neutral")
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var msg = new ChatMessage { Text = text, Mood = mood, Timestamp = DateTime.Now };
            ChatMessages.Add(msg);
            ScrollToEnd();

            // disparition au bout d’1 minute (effet Thanos côté panel/binding XAML)
            _ = ExpireMessageAsync(msg, TimeSpan.FromMinutes(1));
        }

        private async Task ExpireMessageAsync(ChatMessage msg, TimeSpan delay)
        {
            try
            {
                await Task.Delay(delay);
                msg.IsExpiring = true;
                await Task.Delay(TimeSpan.FromMilliseconds(950));
                ChatMessages.Remove(msg);
            }
            catch { /* ignore */ }
        }

        private void ScrollToEnd()
        {
            if (ChatList.Items.Count == 0) return;
            ChatList.ScrollIntoView(ChatList.Items[^1]);
        }

        private void SetAvatarMood(string mood)
        {
            try
            {
                var vm = AvatarControl?.DataContext;
                vm?.GetType().GetMethod("SetMood")?.Invoke(vm, new object[] { mood });
            }
            catch { /* ignore */ }
        }

        // ================== PROGRESSION / ETAT ==================
        private void Progress(double percent, string status, string mood = "vigilant")
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = percent;
            StatusText.Text = status;
            Say(status, mood);
            SetAvatarMood(percent >= 100 ? "proud" : "vigilant");
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

        // ================== SURVEILLANCE ==================
       private void SurveillancePulse()
{
    Say(Dialogues.PulseLineByTimeOfDay(), "vigilant");

    if (_monitoringService == null) return;

    var m = _monitoringService.ReadInstant();
    CpuUsage = m.CpuUsage;
    MemUsage = m.MemoryUsage;
    GpuUsage = m.GpuUsage;
    DiskUsage = m.DiskUsage;

    CpuTempText = m.CpuTempC.HasValue ? $"CPU: {m.CpuTempC.Value:F0} °C" : "CPU: —";
    GpuTempText = m.GpuTempC.HasValue ? $"GPU: {m.GpuTempC.Value:F0} °C" : "GPU: —";
    DiskTempText = m.DiskTempC.HasValue ? $"Disque: {m.DiskTempC.Value:F0} °C" : "Disque: —";
    OnPropertyChanged(nameof(CpuTempText));
    OnPropertyChanged(nameof(GpuTempText));
    OnPropertyChanged(nameof(DiskTempText));

    var c = _config.Current;
    if ((m.CpuTempC.HasValue && m.CpuTempC.Value >= c.CpuTempAlert) ||
        (m.GpuTempC.HasValue && m.GpuTempC.Value >= c.GpuTempAlert))
    {
        Say(Dialogues.AlertTemp(), "alert");
        SetAvatarMood("alert");
    }
}



        private void SurveillancePulse()
        {
            // Punchline liée à l’heure
            Say(Dialogues.PulseLineByTimeOfDay(), "vigilant");

            if (_monitoringService == null) return;

            // Mesures “live”
            var m = _monitoringService.ReadInstant(); // assure-toi que cette méthode existe (sinon utilise la tienne)
            CpuUsage = m.CpuUsage;
            MemUsage = m.MemoryUsage;
            GpuUsage = m.GpuUsage;
            DiskUsage = m.DiskUsage;

            CpuTempText = m.CpuTempC.HasValue ? $"CPU: {m.CpuTempC.Value:F0} °C" : "CPU: —";
            GpuTempText = m.GpuTempC.HasValue ? $"GPU: {m.GpuTempC.Value:F0} °C" : "GPU: —";
            DiskTempText = m.DiskTempC.HasValue ? $"Disque: {m.DiskTempC.Value:F0} °C" : "Disque: —";
            OnPropertyChanged(nameof(CpuTempText));
            OnPropertyChanged(nameof(GpuTempText));
            OnPropertyChanged(nameof(DiskTempText));

            // Réactions température
            var c = _config.Current;
            if ((m.CpuTempC.HasValue && m.CpuTempC.Value >= c.CpuTempAlert) ||
                (m.GpuTempC.HasValue && m.GpuTempC.Value >= c.GpuTempAlert))
            {
                Say(Dialogues.AlertTemp(), "alert");
                SetAvatarMood("alert");
            }
        }

        // ================== ACTIONS (boutons) ==================
        private async void QuickMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, Dialogues.Action("maintenance_quick_start"));

                Progress(10, Dialogues.Action("clean_temp_start"));
                await Task.Run(CleanTempWithProgressInternal);

                Progress(50, Dialogues.Action("clean_browsers_start"));
                var browsers = new BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                Say(Dialogues.Action("clean_browsers_done") + $" (~{rep.BytesDeleted / (1024.0 * 1024):F1} MB)");

                ProgressDone(Dialogues.Action("maintenance_quick_done"));
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"{Dialogues.Action("error_prefix")} {ex.Message}", "alert");
            }
        }

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

                // Jeux (Steam/Epic)
                var games = new GameUpdateService();
                var gOut = await games.UpdateAllAsync();
                if (!string.IsNullOrWhiteSpace(gOut)) Say(gOut);

                // Windows Update
                ProgressIndeterminate(Dialogues.Action("update_windows_start"));
                var wu = new WindowsUpdateService();
                await wu.StartScanAsync();
                await wu.StartDownloadAsync();
                await wu.StartInstallAsync();
                Say(Dialogues.Action("update_windows_done"));

                // Pilotes
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

        // ================== Nettoyage TEMP avec progression réelle ==================
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
                catch { /* locked */ }

                done++;
                var p = Math.Floor(done / total * 100);
                Dispatcher.Invoke(() => Progress(p, $"Nettoyage TEMP… {p:0}%"));
            }

            // Dossiers
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
            {
                Say($"TEMP analysé ~{bytesFound / (1024.0 * 1024):F1} MB — supprimé ~{bytesDeleted / (1024.0 * 1024):F1} MB", "proud");
            });
        }
    }

    // ================== Dialogues centralisés ==================
    static class Dialogues
    {
        private static readonly Random R = new();
        private static dynamic? _data;

        static Dialogues()
        {
            try
            {
                var file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "virgil-dialogues.json");
                if (File.Exists(file))
                {
                    _data = System.Text.Json.JsonSerializer.Deserialize<dynamic>(File.ReadAllText(file));
                }
            }
            catch { /* ignore */ }
        }

        private static string Pick(string section)
        {
            try
            {
                var arr = _data?[section];
                if (arr == null) return "…";
                var list = ((System.Text.Json.Nodes.JsonArray)arr).Select(x => x?.ToString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (list.Count == 0) return "…";
                return list[R.Next(list.Count)];
            }
            catch { return "…"; }
        }

        public static string Startup() => Pick("startup");
        public static string SurveillanceStart() => Pick("surveillance_start");
        public static string SurveillanceStop() => Pick("surveillance_stop");
        public static string PulseLineByTimeOfDay()
        {
            var h = DateTime.Now.Hour;
            if (h is >= 6 and < 12) return Pick("time_morning");
            if (h is >= 12 and < 18) return Pick("time_afternoon");
            if (h is >= 18 and < 23) return Pick("time_evening");
            return Pick("time_night");
        }
        public static string AlertTemp() => Pick("alert_temp");

        public static string Action(string key) => Pick($"action_{key}");
    }
}
