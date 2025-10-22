#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Serilog.Events;
using Virgil.App.Controls;
using Virgil.Core;
using CoreServices = Virgil.Core.Services;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly MonitoringService? _monitoringService;
        private readonly CoreServices.ConfigService _config;
        private bool _isMonitoring;
        private readonly VirgilAvatarViewModel _avatarViewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            _config = new CoreServices.ConfigService();
            CoreServices.LoggingService.Init(LogEventLevel.Information);

            try
            {
                _monitoringService = new MonitoringService();
                _monitoringService.MetricsUpdated += OnMetricsUpdated;
            }
            catch { _monitoringService = null; }

            try
            {
                AvatarControl.DataContext = _avatarViewModel;
                _avatarViewModel.SetMood("neutral", "startup");
                AppendLine("Prêt.");
            }
            catch { }
        }

        // === Helpers ===
        private void Append(string text) { OutputBox.AppendText(text); OutputBox.ScrollToEnd(); }
        private void AppendLine(string line) { OutputBox.AppendText($"[{DateTime.Now:T}] {line}\n"); OutputBox.ScrollToEnd(); }

        private void Say(string text, string mood = "neutral", string context = "general")
        {
            try { _avatarViewModel.SetMood(mood, context); } catch { }
            AppendLine(text);
            StatusText.Text = text;
        }

        private void SetAvatarProgress(double percent)
        {
            try { _avatarViewModel.SetProgress(percent); } catch { }
        }

        private void Progress(double percent, string status)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = percent;
            StatusText.Text = status;
            SetAvatarProgress(percent);
            _avatarViewModel.SetMood(percent >= 100 ? "proud" : "vigilant", "general");
            AppendLine($"[Progress] {percent:0}% — {status}");
        }

        private void ProgressIndeterminate(string status)
        {
            TaskProgress.IsIndeterminate = true;
            StatusText.Text = status;
            SetAvatarProgress(50);
            _avatarViewModel.SetMood("vigilant", "general");
            AppendLine($"[Progress…] {status}");
        }

        private void ProgressDone(string status = "Terminé.")
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 100;
            StatusText.Text = status;
            SetAvatarProgress(100);
            _avatarViewModel.SetMood("proud", "general");
            AppendLine(status);
        }

        private void ProgressReset()
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 0;
            StatusText.Text = "Prêt.";
            _avatarViewModel.SetMood("neutral", "general");
        }

        // === Maintenance ===
        private async void QuickMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Maintenance rapide…");
                Progress(10, "Nettoyage TEMP…");
                await Task.Delay(100);
                await Task.Run(CleanTempWithProgressInternal);

                Progress(50, "Navigateurs (caches)…");
                var browsers = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }));
                AppendLine($"[Browsers] {rep}");
                ProgressDone("Maintenance rapide : terminé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur maintenance rapide: {ex.Message}", "alert"); }
        }

        private async void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Maintenance complète : démarrage…");
                Progress(10, "Nettoyage TEMP…");
                await Task.Run(CleanTempWithProgressInternal);

                Progress(30, "Navigateurs (caches)…");
                var browsers = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }));
                AppendLine($"[Browsers] {rep}");

                Progress(50, "Nettoyage étendu…");
                var ext = new CoreServices.ExtendedCleaningService();
                var exRep = await Task.Run(() => ext.AnalyzeAndClean());
                AppendLine($"[Extended] ~{exRep.BytesFound / (1024.0 * 1024):F1} MB → ~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB");

                ProgressIndeterminate("MAJ apps/jeux (winget)…");
                var app = new ApplicationUpdateService(); // ✅ namespace corrigé
                var wingetOut = await WingetWithRoughProgress(app);
                if (!string.IsNullOrWhiteSpace(wingetOut)) Append(wingetOut);

                ProgressIndeterminate("Windows Update (scan/download/install)…");
                var wu = new WindowsUpdateService(); // ✅ namespace corrigé
                AppendLine("[WU] Scan…"); await wu.StartScanAsync();
                Progress(86, "WU: Download…"); await wu.StartDownloadAsync();
                Progress(93, "WU: Install…"); var install = await wu.StartInstallAsync();
                if (!string.IsNullOrWhiteSpace(install)) Append(install);

                ProgressDone("Maintenance complète : terminé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur maintenance complète: {ex.Message}", "alert"); }
        }

        private async Task<string> WingetWithRoughProgress(ApplicationUpdateService app) // ✅ namespace corrigé
        {
            Progress(5, "Winget: inventaire…"); await Task.Delay(250);
            var output = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
            Progress(65, "Winget: installation…"); await Task.Delay(200);
            Progress(92, "Winget: finalisation…"); await Task.Delay(200);
            return output;
        }

        // === Nettoyages ===
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try { Progress(0, "Nettoyage TEMP…"); await Task.Run(CleanTempWithProgressInternal); ProgressDone("Nettoyage TEMP terminé."); }
            catch (Exception ex) { ProgressReset(); Say($"Erreur nettoyage: {ex.Message}", "alert"); }
        }

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
            foreach (var f in files)
            {
                try
                {
                    var fi = new FileInfo(f);
                    File.SetAttributes(f, FileAttributes.Normal);
                    fi.Delete();
                }
                catch { }
                done++;
                var p = Math.Floor(done / total * 100);
                Dispatcher.Invoke(() => Progress(p, $"Nettoyage TEMP… {p:0}%"));
            }
        }

        private async void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Navigateurs: scan…");
                var svc = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => svc.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }));
                Progress(100, "Navigateurs: terminé.");
                AppendLine($"Caches navigateurs: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                Say("Caches navigateurs nettoyés.", "proud");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur nettoyage navigateurs: {ex.Message}", "alert"); }
        }

        private async void CleanExtendedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Nettoyage étendu…");
                var ext = new CoreServices.ExtendedCleaningService();
                var rep = await Task.Run(() => ext.AnalyzeAndClean());
                Progress(100, "Nettoyage étendu terminé.");
                Say("Nettoyage étendu : ok.", "proud");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur Clean Extended: {ex.Message}", "alert"); }
        }

        // === Mises à jour ===
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("MAJ apps/jeux (winget)…");
                var app = new ApplicationUpdateService(); // ✅ namespace corrigé
                var txt = await WingetWithRoughProgress(app);
                if (!string.IsNullOrWhiteSpace(txt)) Append(txt);
                ProgressDone("MAJ apps/jeux terminées.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur MAJ apps: {ex.Message}", "alert"); }
        }

        private async void DriverButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("MAJ pilotes (best-effort)…");
                var drv = new DriverUpdateService(); // ✅ namespace corrigé
                var output = await drv.UpgradeDriversAsync();
                if (!string.IsNullOrWhiteSpace(output)) Append(output);
                ProgressDone("Pilotes : vérification terminée.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur pilotes: {ex.Message}", "alert"); }
        }

        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try { Progress(10, "WU: Scan…"); var s = await new WindowsUpdateService().StartScanAsync(); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Scan demandé."); }
            catch (Exception ex) { ProgressReset(); Say($"WU Scan: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try { Progress(10, "WU: Download…"); var s = await new WindowsUpdateService().StartDownloadAsync(); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Download demandé."); }
            catch (Exception ex) { ProgressReset(); Say($"WU Download: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateInstallButton_Click(object sender, RoutedEventArgs e)
        {
            try { Progress(10, "WU: Install…"); var s = await new WindowsUpdateService().StartInstallAsync(); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Install demandé."); }
            catch (Exception ex) { ProgressReset(); Say($"WU Install: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateRestartButton_Click(object sender, RoutedEventArgs e)
        {
            try { Progress(10, "WU: Restart…"); var s = await new WindowsUpdateService().RestartDeviceAsync(); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Restart demandé."); }
            catch (Exception ex) { ProgressReset(); Say($"WU Restart: {ex.Message}", "alert"); }
        }

        // === Reste (monitoring, services, outils) ===
        // (inchangé)
        // …
    }
}
