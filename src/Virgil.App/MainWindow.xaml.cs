#nullable enable
using Virgil.App.Controls;   // pour VirgilAvatarViewModel (existant)
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        private CancellationTokenSource? _cts;

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
                if (AvatarControl != null && AvatarControl.DataContext is VirgilAvatarViewModel vm)
                {
                    vm.SetMood("neutral", "startup");
                }
            }
            catch { }
        }

        // ===== Helpers
        private void Append(string text) { OutputBox.AppendText(text); OutputBox.ScrollToEnd(); }
        private void AppendLine(string line) { OutputBox.AppendText($"[{DateTime.Now:T}] {line}\n"); OutputBox.ScrollToEnd(); }

        private void Progress(double percent, string status)
        {
            if (percent < 0) percent = 0; if (percent > 100) percent = 100;
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = percent;
            StatusText.Text = status;

            if (AvatarControl?.DataContext is VirgilAvatarViewModel vm)
                vm.SetProgress(percent / 100.0, status);
        }
        private void ProgressIndeterminate(string status)
        {
            TaskProgress.IsIndeterminate = true;
            StatusText.Text = status;

            if (AvatarControl?.DataContext is VirgilAvatarViewModel vm)
                vm.SetProgress(-1, status); // -1 = indéterminé
        }
        private void ProgressDone(string status = "Terminé.")
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 100;
            StatusText.Text = status;

            if (AvatarControl?.DataContext is VirgilAvatarViewModel vm)
            {
                vm.SetProgress(1.0, status);
                vm.SetMood("proud", "done");
            }

            AppendLine(status);
        }
        private void ProgressReset()
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 0;
            StatusText.Text = "Prêt.";

            if (AvatarControl?.DataContext is VirgilAvatarViewModel vm)
            {
                vm.SetProgress(0.0, "Prêt.");
                vm.SetMood("neutral", "reset");
            }
        }
        private void Say(string text, string mood = "neutral", string context = "general")
        {
            if (AvatarControl?.DataContext is VirgilAvatarViewModel vm)
                vm.SetMood(mood, context);

            AppendLine(text);
        }

        private bool IsCancelled(CancellationToken token)
        {
            if (!token.IsCancellationRequested) return false;
            ProgressReset();
            Say("Opération annulée.", "neutral", "cancel");
            return true;
        }

        // ===== Bouton Annuler
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try { _cts?.Cancel(); } catch { }
        }

        // ===== Maintenance
        private async void QuickMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Progress(0, "Maintenance rapide…");

                Progress(10, "TEMP : analyse/suppression…");
                if (IsCancelled(token)) return;
                await Task.Delay(100, token);
                await Task.Run(CleanTempWithProgressInternal, token);

                Progress(50, "Navigateurs : caches…");
                if (IsCancelled(token)) return;
                var browsers = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }), token);
                AppendLine($"[Browsers] {rep.BytesDeleted / (1024.0 * 1024):F1} MB supprimés");

                // Apps
                ProgressIndeterminate("MAJ apps/jeux (winget) …");
                if (IsCancelled(token)) return;
                var app = new CoreServices.ApplicationUpdateService();
                var wingetOut = await Task.Run(() => app.UpgradeAllAsync(includeUnknown: true, silent: true), token);
                Append(await wingetOut);

                ProgressDone("Maintenance rapide : terminé.");
            }
            catch (OperationCanceledException) { /* handled by IsCancelled */ }
            catch (Exception ex) { ProgressReset(); Say($"Erreur maintenance rapide: {ex.Message}", "alert"); }
        }

        private async void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Progress(0, "Maintenance complète : démarrage…");

                Progress(10, "TEMP : analyse/suppression…");
                if (IsCancelled(token)) return;
                await Task.Run(CleanTempWithProgressInternal, token);

                Progress(30, "Navigateurs : caches…");
                if (IsCancelled(token)) return;
                var browsers = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }), token);
                AppendLine($"[Browsers] {rep.BytesDeleted / (1024.0 * 1024):F1} MB supprimés");

                Progress(50, "Nettoyage étendu…");
                if (IsCancelled(token)) return;
                var ext = new CoreServices.ExtendedCleaningService();
                var exRep = await Task.Run(() => ext.AnalyzeAndClean(), token);
                AppendLine($"[Extended] ~{exRep.BytesFound / (1024.0 * 1024):F1} MB → ~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB");

                ProgressIndeterminate("MAJ apps/jeux (winget) …");
                if (IsCancelled(token)) return;
                var app = new CoreServices.ApplicationUpdateService();
                var wingetOut = await Task.Run(() => app.UpgradeAllAsync(includeUnknown: true, silent: true), token);
                Append(await wingetOut);

                Progress(86, "WU: Scan…");
                if (IsCancelled(token)) return;
                var wu = new CoreServices.WindowsUpdateService();
                Append(await wu.StartScanAsync());

                Progress(93, "WU: Download/Install…");
                if (IsCancelled(token)) return;
                Append(await wu.StartDownloadAsync());
                Append(await wu.StartInstallAsync());

                ProgressDone("Maintenance complète : terminé.");
            }
            catch (OperationCanceledException) { /* handled by IsCancelled */ }
            catch (Exception ex) { ProgressReset(); Say($"Erreur maintenance complète: {ex.Message}", "alert"); }
        }

        // ===== Nettoyage TEMP (progression réelle)
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Progress(0, "Nettoyage TEMP…");
                await Task.Run(CleanTempWithProgressInternal, token);
                ProgressDone("Nettoyage TEMP terminé.");
            }
            catch (OperationCanceledException) { /* handled */ }
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

            Dispatcher.Invoke(() =>
            {
                AppendLine($"TEMP détecté ~{bytesFound / (1024.0 * 1024):F1} MB — supprimé ~{bytesDeleted / (1024.0 * 1024):F1} MB, {deleted} fichiers.");
            });
        }

        // ===== Nettoyages complémentaires
        private async void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Progress(0, "Navigateurs: scan…");
                var svc = new CoreServices.BrowserCleaningService();
                var rep = await Task.Run(() => svc.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false }), token);
                Progress(100, "Navigateurs: terminé.");
                AppendLine($"Caches navigateurs supprimés: ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                Say("Caches navigateurs nettoyés.", "proud", "clean browsers");
            }
            catch (OperationCanceledException) { /* handled */ }
            catch (Exception ex) { ProgressReset(); Say($"Erreur nettoyage navigateurs: {ex.Message}", "alert"); }
        }

        private async void CleanExtendedButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                Progress(0, "Nettoyage étendu…");
                var ext = new CoreServices.ExtendedCleaningService();
                var rep = await Task.Run(() => ext.AnalyzeAndClean(), token);
                Progress(100, "Nettoyage étendu terminé.");
                AppendLine($"Étendu: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                Say("Nettoyage étendu : ok.", "proud", "clean extended");
            }
            catch (OperationCanceledException) { /* handled */ }
            catch (Exception ex) { ProgressReset(); Say($"Erreur Clean Extended: {ex.Message}", "alert"); }
        }

        // ===== Mises à jour
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                ProgressIndeterminate("MAJ apps/jeux (winget)…");
                var app = new CoreServices.ApplicationUpdateService();
                var txt = await Task.Run(() => app.UpgradeAllAsync(includeUnknown: true, silent: true), token);
                Append(await txt);
                ProgressDone("MAJ apps/jeux terminées.");
            }
            catch (OperationCanceledException) { /* handled */ }
            catch (Exception ex) { ProgressReset(); Say($"Erreur MAJ apps: {ex.Message}", "alert", "maj apps"); }
        }

        private async void DriverButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                ProgressIndeterminate("MAJ pilotes (best-effort) …");
                var drv = new CoreServices.DriverUpdateService();
                var output = await Task.Run(() => drv.UpgradeDriversAsync(), token);
                if (!string.IsNullOrWhiteSpace(output)) Append(output);
                ProgressDone("Pilotes : vérification terminée.");
            }
            catch (OperationCanceledException) { /* handled */ }
            catch (Exception ex) { ProgressReset(); Say($"Erreur pilotes: {ex.Message}", "alert", "maj pilotes"); }
        }

        // ===== Windows Update (UsoClient)
        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try { Progress(10, "WU: Scan…"); var s = await Task.Run(() => new CoreServices.WindowsUpdateService().StartScanAsync(), token); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Scan demandé."); }
            catch (OperationCanceledException) { /* handled */ }
            catch (Exception ex) { ProgressReset(); Say($"WU Scan: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try { Progress(10, "WU: Download…"); var s = await Task.Run(() => new CoreServices.WindowsUpdateService().StartDownloadAsync(), token); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Download demandé."); }
            catch (OperationCanceledException) { /* handled */ }
            catch (Exception ex) { ProgressReset(); Say($"WU Download: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateInstallButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try { Progress(10, "WU: Install…"); var s = await Task.Run(() => new CoreServices.WindowsUpdateService().StartInstallAsync(), token); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Install demandé."); }
            catch (OperationCanceledException) { /* handled */ }
            catch (Exception ex) { ProgressReset(); Say($"WU Install: {ex.Message}", "alert"); }
        }
        private async void WindowsUpdateRestartButton_Click(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try { Progress(10, "WU: Restart…"); var s = await Task.Run(() => new CoreServices.WindowsUpdateService().RestartDeviceAsync(), token); if (!string.IsNullOrWhiteSpace(s)) Append(s); ProgressDone("WU: Restart demandé."); }
            catch (OperationCanceledException) { /* handled */ }
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
                    DecideMoodFromTemps(s), "temps read");
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
                Say("Top 15 processus par RAM :", "vigilant", "process list");
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

        // ===== Monitoring callback
        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            if (_monitoringService == null) return;
            var m = _monitoringService.LatestMetrics;
            Dispatcher.Invoke(() => AppendLine($"CPU: {m.CpuUsage:F1}%  MEM: {m.MemoryUsage:F1}%"));

            var c = _config.Current;
            if (m.CpuUsage >= c.CpuAlert || m.MemoryUsage >= c.MemAlert)      Say("Charge élevée.", "alert", "charge élevée");
            else if (m.CpuUsage >= c.CpuWarn || m.MemoryUsage >= c.MemWarn)   Say("Charge modérée.", "vigilant", "charge modérée");
            else                                                              Say("Charge normale.", "neutral", "charge normale");
        }
    }
}
