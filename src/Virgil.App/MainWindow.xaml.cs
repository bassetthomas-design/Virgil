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

using Serilog.Events;
using Virgil.Core.Services;
using Virgil.App.Controls; // pour VirgilChatPanel

namespace Virgil.App
{
    public class ChatMessage : INotifyPropertyChanged
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;

        private string _text = "";
        public string Text { get => _text; set { _text = value; OnPropertyChanged(); } }

        private string _mood = "neutral";
        public string Mood { get => _mood; set { _mood = value; OnPropertyChanged(); } }

        public System.Windows.Media.Brush BubbleBrush { get; private set; } =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));

        private bool _isExpiring;
        public bool IsExpiring { get => _isExpiring; set { _isExpiring = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // === Bindings UI
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();
        public bool IsSurveillanceOn
        {
            get => _isMonitoring;
            set { _isMonitoring = value; OnPropertyChanged(); UpdateSurveillanceState(); }
        }
        public string SurveillanceButtonText => IsSurveillanceOn ? "Arrêter la surveillance" : "Démarrer la surveillance";

        // Stats panneau
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

        private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _survTimer  = new() { Interval = TimeSpan.FromSeconds(10) };

        // Services
        private readonly AdvancedMonitoringService _adv = new();
        private readonly UsageProbe _probe = new();

        private bool _isMonitoring;

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();

            // Appel de LoggingService.Init(LogEventLevel.Information) si disponible (réflexion)
            try
            {
                var t = Type.GetType("Virgil.Core.LoggingService, Virgil.Core");
                var m = t?.GetMethod("Init", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m != null) m.Invoke(null, new object[] { LogEventLevel.Information });
            }
            catch { /* pas d'Init dans cette version → on ignore */ }

            // Horloge
            _clockTimer.Tick += (_, __) => { ClockText.Text = DateTime.Now.ToString("dddd dd MMM HH:mm"); };
            _clockTimer.Start();

            // Surveillance (chat + stats)
            _survTimer.Tick += (_, __) => SurveillancePulse();

            // Accueil
            Say("Virgil en place. Système prêt.");
        }

        // ================== CHAT (via panneau custom) ==================
        private void Say(string text, string mood = "neutral", int ttlMs = 60000)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            // ChatArea est un <controls:VirgilChatPanel x:Name="ChatArea" /> dans MainWindow.xaml
            ChatArea.Post(text, mood, ttlMs);
        }

        // ================== PROGRESSION / ETAT ==================
        private void Progress(double percent, string status, string mood = "vigilant")
        {
            percent = Math.Max(0, Math.Min(100, percent));
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = percent;
            StatusText.Text = status;
            Say(status, mood);
        }
        private void ProgressIndeterminate(string status, string mood = "vigilant")
        {
            TaskProgress.IsIndeterminate = true;
            StatusText.Text = status;
            Say(status, mood);
        }
        private void ProgressDone(string status = "Terminé.")
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 100;
            StatusText.Text = status;
            Say(status, "proud");
        }
        private void ProgressReset()
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 0;
            StatusText.Text = "Prêt.";
        }

        // ================== SURVEILLANCE ==================
        private void UpdateSurveillanceState()
        {
            OnPropertyChanged(nameof(SurveillanceButtonText));
            if (IsSurveillanceOn)
            {
                Say("Surveillance activée. Je garde un œil 👀", "vigilant");
                _survTimer.Start();
                SurveillancePulse(); // premier tick immédiat
            }
            else
            {
                _survTimer.Stop();
                Say("Surveillance arrêtée.", "neutral");
            }
        }

        private void SurveillancePulse()
        {
            // petite punchline selon l’heure
            var h = DateTime.Now.Hour;
            if      (h is >= 6 and < 12) Say("☀️ Bonjour ! Tout roule.", "vigilant", 15000);
            else if (h is >= 12 and < 18) Say("🛡️ Je surveille pendant que tu bosses.", "vigilant", 15000);
            else if (h is >= 18 and < 23) Say("🌇 Fin de journée ? Je garde l’œil.", "vigilant", 15000);
            else                           Say("🌙 Nuit calme, je veille.", "vigilant", 15000);

            // usages (CPU/GPU/Mémoire/Disque)
            var u = _probe.Read();
            CpuUsage = u.cpu;
            MemUsage = u.mem;
            DiskUsage = u.disk;
            GpuUsage = u.gpu; // peut rester 0 si pas dispo
            OnPropertyChanged(nameof(CpuUsage));
            OnPropertyChanged(nameof(MemUsage));
            OnPropertyChanged(nameof(DiskUsage));
            OnPropertyChanged(nameof(GpuUsage));

            // températures via AdvancedMonitoringService
            var t = _adv.Read();
            CpuTempText = t.CpuTempC.HasValue ? $"CPU: {t.CpuTempC.Value:F0} °C" : "CPU: —";
            GpuTempText = t.GpuTempC.HasValue ? $"GPU: {t.GpuTempC.Value:F0} °C" : "GPU: —";
            DiskTempText = t.DiskTempC.HasValue ? $"Disque: {t.DiskTempC.Value:F0} °C" : "Disque: —";
            OnPropertyChanged(nameof(CpuTempText));
            OnPropertyChanged(nameof(GpuTempText));
            OnPropertyChanged(nameof(DiskTempText));

            // alerte température simple (seuils fixes — ajuste si tu as de la config)
            if ((t.CpuTempC ?? 0) >= 85 || (t.GpuTempC ?? 0) >= 85)
                Say("🔥 Ça chauffe un peu. Pense à ventiler.", "alert", 12000);
        }

        // ================== ACTIONS (boutons) ==================
        private async void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Maintenance complète en cours…");

                Progress(10, "Nettoyage TEMP…");
                await Task.Run(CleanTempWithProgressInternal);

                Progress(30, "Nettoyage navigateurs…");
                var browsers = new BrowserCleaningService();
                var bRep = await Task.Run(() => browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                Say($"Caches navigateurs effacés (~{bRep.BytesDeleted / (1024.0 * 1024):F1} MB).");

                Progress(50, "Nettoyage étendu…");
                var ext = new ExtendedCleaningService();
                var exRep = await Task.Run(() => ext.AnalyzeAndClean());
                Say($"Nettoyage étendu ok (~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB).");

                ProgressIndeterminate("Mise à jour apps/jeux…");
                var app = new ApplicationUpdateService();
                var txt = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
                if (!string.IsNullOrWhiteSpace(txt)) Say("Apps mises à jour.");

                var games = new GameUpdateService();
                var gOut = await games.UpdateAllAsync();
                if (!string.IsNullOrWhiteSpace(gOut)) Say(gOut);

                ProgressIndeterminate("Windows Update…");
                var wu = new WindowsUpdateService();
                await wu.StartScanAsync();
                await wu.StartDownloadAsync();
                await wu.StartInstallAsync();
                Say("Windows est à jour.");

                ProgressIndeterminate("Pilotes…");
                var drv = new DriverUpdateService();
                var dOut = await drv.UpgradeDriversAsync();
                if (!string.IsNullOrWhiteSpace(dOut)) Say("Pilotes mis à jour.");

                ProgressDone("Maintenance complète terminée.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur: {ex.Message}", "alert");
            }
        }

        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Nettoyage TEMP…");
                await Task.Run(CleanTempWithProgressInternal);
                ProgressDone("Nettoyage terminé.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur: {ex.Message}", "alert");
            }
        }

        private async void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Nettoyage navigateurs…");
                var svc = new BrowserCleaningService();
                var rep = await Task.Run(() => svc.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                ProgressDone($"Navigateurs propres (~{rep.BytesDeleted / (1024.0 * 1024):F1} MB).");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur: {ex.Message}", "alert");
            }
        }

        private async void UpdateAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("Mise à jour apps/jeux/Windows/pilotes…");

                var app = new ApplicationUpdateService();
                var txt = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
                if (!string.IsNullOrWhiteSpace(txt)) Say("Apps mises à jour.");

                var games = new GameUpdateService();
                var gOut = await games.UpdateAllAsync();
                if (!string.IsNullOrWhiteSpace(gOut)) Say(gOut);

                var drv = new DriverUpdateService();
                var dOut = await drv.UpgradeDriversAsync();
                if (!string.IsNullOrWhiteSpace(dOut)) Say("Pilotes mis à jour.");

                var wu = new WindowsUpdateService();
                await wu.StartScanAsync();
                await wu.StartDownloadAsync();
                await wu.StartInstallAsync();
                Say("Windows est à jour.");

                ProgressDone("Tout est à jour.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur: {ex.Message}", "alert");
            }
        }

        // ================== TEMP cleaner avec progression ==================
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
                Dispatcher.Invoke(() =>
                {
                    TaskProgress.IsIndeterminate = false;
                    TaskProgress.Value = p;
                    StatusText.Text = $"Nettoyage TEMP… {p:0}%";
                });
            }

            Dispatcher.Invoke(() =>
                Say($"TEMP analysé ~{bytesFound / (1024.0 * 1024):F1} MB — supprimé ~{bytesDeleted / (1024.0 * 1024):F1} MB", "proud"));
        }
    }

    /// <summary>
    /// Petit lecteur d’usages (CPU/GPU/Mémoire/Disque) sans dépendances externes.
    /// GPU à 0 par défaut (à remplacer si tu as un compteur adapté).
    /// </summary>
    internal sealed class UsageProbe
    {
        private readonly PerformanceCounter _cpu = new("Processor", "% Processor Time", "_Total", true);
        private readonly PerformanceCounter _disk = new("PhysicalDisk", "% Disk Time", "_Total", true);

        public (double cpu, double gpu, double mem, double disk) Read()
        {
            double cpu = SafeRead(_cpu);
            double disk = SafeRead(_disk);

            // mémoire utilisée
            var pc = new Microsoft.VisualBasic.Devices.ComputerInfo();
            double memUsed = (pc.TotalPhysicalMemory - pc.AvailablePhysicalMemory) / (double)pc.TotalPhysicalMemory * 100.0;

            double gpu = 0; // si tu as un compteur GPU, injecte-le ici

            return (Clamp(cpu), Clamp(gpu), Clamp(memUsed), Clamp(disk));
        }

        private static double SafeRead(PerformanceCounter c)
        {
            try { return c.NextValue(); } catch { return 0; }
        }
        private static double Clamp(double v) => Math.Max(0, Math.Min(100, double.IsFinite(v) ? v : 0));
    }
}
