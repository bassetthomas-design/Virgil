#nullable enable
using Virgil.App.Controls;   // VirgilAvatarViewModel
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Serilog.Events;
using Virgil.Core;
using CoreServices = Virgil.Core.Services;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly MonitoringService? _monitoringService;
        private readonly CoreServices.ConfigService _config;
        private bool _isMonitoring;

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

            // Avatar
            try
            {
                var vm = new VirgilAvatarViewModel();
                AvatarControl.DataContext = vm;
                Say("Prêt.", "neutral");
            }
            catch { }
        }

        // ===== Helpers
        private VirgilAvatarViewModel? AvatarVm => AvatarControl?.DataContext as VirgilAvatarViewModel;

        private void Append(string text) { OutputBox.AppendText(text); OutputBox.ScrollToEnd(); }
        private void AppendLine(string line) { OutputBox.AppendText($"[{DateTime.Now:T}] {line}\n"); OutputBox.ScrollToEnd(); }

        private void Say(string text, string mood = "neutral")
        {
            try
            {
                AvatarVm?.SetMessage(text);
                AvatarVm?.SetMood(mood);
            }
            catch { }
            AppendLine(text);
        }

        private void SetUiProgress(double percent, string status, bool indeterminate = false)
        {
            TaskProgress.IsIndeterminate = indeterminate;
            if (!indeterminate) TaskProgress.Value = Math.Max(0, Math.Min(100, percent));
            StatusText.Text = status;
            AvatarVm?.SetProgress(indeterminate ? 0 : percent, indeterminate);
            AvatarVm?.SetMood(percent >= 100 && !indeterminate ? "proud" : "vigilant");
            if (!string.IsNullOrWhiteSpace(status)) AvatarVm?.SetMessage(status);
        }

        private void Progress(double percent, string status)
            => SetUiProgress(percent, status, indeterminate: false);
        private void ProgressIndeterminate(string status)
            => SetUiProgress(0, status, indeterminate: true);
        private void ProgressDone(string status = "Terminé.")
            => SetUiProgress(100, status, indeterminate: false);
        private void ProgressReset()
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 0;
            StatusText.Text = "Prêt.";
            AvatarVm?.SetProgress(0, false);
            AvatarVm?.SetMood("neutral");
            AvatarVm?.SetMessage("Prêt.");
        }

        // ===== Maintenance
        private async void QuickMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Maintenance rapide…");

                Progress(10, "Nettoyage TEMP…");
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

                Progress(30, "Navigateurs (caches) …");
                var browsers = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }));
                AppendLine($"[Browsers] {rep}");

                Progress(50, "Nettoyage étendu…");
                var ext = new CoreServices.ExtendedCleaningService();
                var exRep = await Task.Run(() => ext.AnalyzeAndClean());
                AppendLine($"[Extended] ~{exRep.BytesFound / (1024.0 * 1024):F1} MB → ~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB");

                ProgressIndeterminate("MAJ apps/jeux (winget) …");
                var app = new CoreServices.ApplicationUpdateService();
                var wingetOut = await WingetWithRoughProgress(app);
                if (!string.IsNullOrWhiteSpace(wingetOut)) Append(wingetOut);

                ProgressIndeterminate("Windows Update (scan/download/install) …");
                var wu = new CoreServices.WindowsUpdateService();
                AppendLine("[WU] Scan…");
                var scan = await wu.StartScanAsync();
                if (!string.IsNullOrWhiteSpace(scan)) Append(scan);

                Progress(86, "WU: Download…");
                var dl = await wu.StartDownloadAsync();
                if (!string.IsNullOrWhiteSpace(dl)) Append(dl);

                Progress(93, "WU: Install…");
                var install = await wu.StartInstallAsync();
                if (!string.IsNullOrWhiteSpace(install)) Append(install);

                ProgressDone("Maintenance complète : terminé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur maintenance complète: {ex.Message}", "alert"); }
        }

        // ===== Nettoyage TEMP (progression réelle)
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try { Progress(0, "Nettoyage TEMP…"); await Task.Run(CleanTempWithProgressInternal); ProgressDone("Nettoyage TEMP terminé."); }
            catch (Exception ex) { ProgressReset(); Say($"Erreur de nettoyage: {ex.Message}", "alert"); }
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
            long bytesFound = 0, bytesDeleted = 0;
            int deleted = 0;

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
                    deleted++;
                    bytesDeleted += len;
                }
                catch { /* ignore locked */ }

                done++;
                var p = Math.Floor(done / total * 100);
                Dispatcher.Invoke(() => Progress(p, $"Nettoyage TEMP… {p:0}%"));
            }

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
                AppendLine($"TEMP détecté ~{bytesFound / (1024.0 * 1024):F1} MB — supprimé ~{bytesDeleted / (1024.0 * 1024):F1} MB, {deleted} fichiers.");
            });
        }

        // ===== Nettoyages complémentaires
        private async void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Navigateurs: scan…");
                var svc = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => svc.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }));
                Progress(100, "Navigateurs: terminé.");
                AppendLine($"Caches navigateurs détectés: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → supprimés: ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
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
                AppendLine($"Étendu: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                Say("Nettoyage étendu : ok.", "proud");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur Clean Extended: {ex.Message}", "alert"); }
        }

        // ===== Mises à jour apps/jeux
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("MAJ apps/jeux (winget)…");
                var app = new CoreServices.ApplicationUpdateService();
                var txt = await WingetWithRoughProgress(app);
                if (!string.IsNullOrWhiteSpace(txt)) Append(txt);
                ProgressDone("MAJ apps/jeux terminées.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur MAJ apps: {ex.Message}", "alert"); }
        }

        private async Task<string> WingetWithRoughProgress(CoreServices.ApplicationUpdateService app)
        {
            Progress(5, "Winget: inventaire…"); await Task.Delay(250);
            var output = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
            Progress(65, "Winget: installation…"); await Task.Delay(200);
            Progress(92, "Winget: finalisation…"); await Task.Delay(200);
            return output ?? string.Empty;
        }

        private async void DriverButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("MAJ pilotes (best-effort)…");
                var drv = new CoreServices.DriverUpdateService();
                var output = await drv.UpgradeDriversAsync();
                if (!string.IsNullOrWhiteSpace(output)) Append(output);
                ProgressDone("Pilotes : vérification terminée.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Erreur pilotes: {ex.Message}", "alert"); }
        }

        // ===== Windows Update (UsoClient)
        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Scan…");
                var s = await new CoreServices.WindowsUpdateService().StartScanAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Scan demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Scan: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Download…");
                var s = await new CoreServices.WindowsUpdateService().StartDownloadAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Download demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Download: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateInstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Install…");
                var s = await new CoreServices.WindowsUpdateService().StartInstallAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Install demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Install: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateRestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Restart…");
                var s = await new CoreServices.WindowsUpdateService().RestartDeviceAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Restart demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Restart: {ex.Message}", "alert"); }
        }

        // ===== Monitoring
        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            _isMonitoring = !_isMonitoring;
            if (_isMonitoring && _monitoringService != null)
            {
                _monitoringService.Start();
                MonitorButton.Content = "Stop Monitoring";
                Say("Monitoring démarré.", "vigilant");
            }
            else
            {
                _monitoringService?.Stop();
                MonitorButton.Content = "Start Monitoring";
                Say("Monitoring arrêté.", "neutral");
            }
        }

        private void ReadTempsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var adv = new CoreServices.AdvancedMonitoringService();
                var s = adv.Read();
                Say($"Températures → CPU: {(s.CpuTempC?.ToString("F0") ?? "?")}°C | GPU: {(s.GpuTempC?.ToString("F0") ?? "?")}°C | Disque: {(s.DiskTempC?.ToString("F0") ?? "?")}°C",
                    DecideMoodFromTemps(s));
            }
            catch (Exception ex) { Say($"Erreur lecture températures: {ex.Message}", "alert"); }
        }

        private string DecideMoodFromTemps(CoreServices.HardwareSnapshot s)
        {
            var c = _config.Current;
            if ((s.CpuTempC.HasValue && s.CpuTempC.Value >= c.CpuTempAlert) ||
                (s.GpuTempC.HasValue && s.GpuTempC.Value >= c.GpuTempAlert))
                return "alert";
            if ((s.CpuTempC.HasValue && s.CpuTempC.Value >= c.CpuTempWarn) ||
                (s.GpuTempC.HasValue && s.GpuTempC.Value >= c.GpuTempWarn))
                return "vigilant";
            return "neutral";
        }

        // ===== Processus
        private void ProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var procs = Process.GetProcesses()
                                   .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0L; } })
                                   .Take(15).ToList();
                Say("Top 15 processus par RAM :", "vigilant");
                foreach (var p in procs)
                {
                    long ws = 0; try { ws = p.WorkingSet64; } catch { }
                    AppendLine($"- {p.ProcessName} (PID {p.Id}) — {ws / (1024.0 * 1024):F1} MB");
                }
            }
            catch (Exception ex) { Say($"Erreur liste des processus: {ex.Message}", "alert"); }
        }

        private void KillPidButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(KillPidBox.Text, out var pid)) { Say("PID invalide.", "alert"); return; }
                try { Process.GetProcessById(pid).Kill(true); Say($"Processus {pid} terminé.", "proud"); }
                catch { Say($"Impossible de terminer {pid} (droits/admin ?).", "alert"); }
            }
            catch (Exception ex) { Say($"Erreur kill: {ex.Message}", "alert"); }
        }

        // ===== Services
        private void ListServicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sm = new CoreServices.ServiceManager();
                var list = sm.ListAll().Take(30).ToList();
                Say("Services (30 premiers) :", "vigilant");
                foreach (var s in list) AppendLine($"- {s.DisplayName} ({s.Name}) — {s.Status}");
            }
            catch (Exception ex) { Say($"Erreur list services: {ex.Message}", "alert"); }
        }

        private void RestartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { Say("Nom de service manquant.", "alert"); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Restart(name);
                Say(ok ? $"Service {name} redémarré." : $"Échec restart {name}.", ok ? "proud" : "alert");
            }
            catch (Exception ex) { Say($"Erreur restart: {ex.Message}", "alert"); }
        }

        private void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { Say("Nom de service manquant.", "alert"); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Start(name);
                Say(ok ? $"Service {name} démarré." : $"Échec start {name}.", ok ? "proud" : "alert");
            }
            catch (Exception ex) { Say($"Erreur start: {ex.Message}", "alert"); }
        }

        private void StopServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { Say("Nom de service manquant.", "alert"); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Stop(name);
                Say(ok ? $"Service {name} arrêté." : $"Échec stop {name}.", ok ? "proud" : "alert");
            }
            catch (Exception ex) { Say($"Erreur stop: {ex.Message}", "alert"); }
        }

        // ===== Outils & Journal
        private void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            try { System.Windows.Clipboard.SetText(OutputBox.Text); Say("Journal copié.", "proud"); }
            catch (Exception ex) { Say($"Clipboard : {ex.Message}", "alert"); }
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "logs");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(file, OutputBox.Text, Encoding.UTF8);
                Say($"Journal exporté : {file}", "proud");
                try { Process.Start("explorer.exe", $"/select,\"{file}\""); } catch { }
            }
            catch (Exception ex) { Say($"Export journal : {ex.Message}", "alert"); }
        }

        private void OpenDeviceManagerButton_Click(object sender, RoutedEventArgs e)
            => TryStart("devmgmt.msc", useShell: true, "Gestionnaire de périphériques");

        private void OpenDiskCleanupButton_Click(object sender, RoutedEventArgs e)
            => TryStart("cleanmgr.exe", useShell: true, "Nettoyage de disque");

        private void OpenServicesConsoleButton_Click(object sender, RoutedEventArgs e)
            => TryStart("services.msc", useShell: true, "Services");

        private async void FlushDnsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("Flush DNS…");
                var psi = new ProcessStartInfo("ipconfig", "/flushdns")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    var output = await p.StandardOutput.ReadToEndAsync();
                    var err = await p.StandardError.ReadToEndAsync();
                    await p.WaitForExitAsync();
                    if (!string.IsNullOrWhiteSpace(output)) Append(output);
                    if (!string.IsNullOrWhiteSpace(err)) Append(err);
                }
                ProgressDone("Flush DNS : ok.");
            }
            catch (Exception ex) { ProgressReset(); Say($"Flush DNS : {ex.Message}", "alert"); }
        }

        private void TryStart(string fileName, bool useShell, string label)
        {
            try
            {
                var psi = new ProcessStartInfo(fileName) { UseShellExecute = useShell };
                Process.Start(psi);
                Say($"Ouverture : {label}", "neutral");
            }
            catch (Exception ex) { Say($"Impossible d’ouvrir {label} : {ex.Message}", "alert"); }
        }

        // ===== Monitoring callback
        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            if (_monitoringService == null) return;
            var m = _monitoringService.LatestMetrics;
            Dispatcher.Invoke(() => AppendLine($"CPU: {m.CpuUsage:F1}%  MEM: {m.MemoryUsage:F1}%"));

            var c = _config.Current;
            if (m.CpuUsage >= c.CpuAlert || m.MemoryUsage >= c.MemAlert)      Say("Charge élevée.", "alert");
            else if (m.CpuUsage >= c.CpuWarn || m.MemoryUsage >= c.MemWarn)   Say("Charge modérée.", "vigilant");
            else                                                              Say("Charge normale.", "neutral");
        }
    }
}
