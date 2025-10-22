#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Serilog.Events;
using Virgil.Core; // MonitoringService + Presets + BrowserCleaning + ExtendedCleaning + Updates
using CoreServices = Virgil.Core.Services; // ConfigService, LoggingService, AdvancedMonitoringService, ServiceManager

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        private readonly Virgil.Core.MonitoringService? _monitoringService;
        private readonly CoreServices.ConfigService _config;
        private bool _isMonitoring;

        public MainWindow()
        {
            InitializeComponent();

            _config = new CoreServices.ConfigService();
            CoreServices.LoggingService.Init(LogEventLevel.Information);

            try
            {
                _monitoringService = new Virgil.Core.MonitoringService();
                _monitoringService.MetricsUpdated += OnMetricsUpdated;
            }
            catch { _monitoringService = null; }

            // Avatar setup
            try
            {
                var vmType = Type.GetType("Virgil.App.Controls.VirgilAvatarViewModel, Virgil.App");
                if (vmType != null && AvatarControl != null)
                {
                    var vm = Activator.CreateInstance(vmType);
                    AvatarControl.DataContext = vm;
                    Say("Prêt.", mood: "neutral", context: "startup");
                }
            }
            catch { }
        }

        // ===== Helpers UI / avatar / progression
        private void Append(string text) { OutputBox.AppendText(text); OutputBox.ScrollToEnd(); }
        private void AppendLine(string line) { OutputBox.AppendText($"[{DateTime.Now:T}] {line}\n"); OutputBox.ScrollToEnd(); }

        private void Progress(double percent, string status)
        {
            if (percent < 0) percent = 0; if (percent > 100) percent = 100;
            TaskProgress.Value = percent;
            StatusText.Text = status;
            // avatar "parle"
            Say($"{status}", mood: percent switch { < 10 => "vigilant", < 90 => "vigilant", _ => "proud" }, context: "general");
        }

        private void ProgressIndeterminate(string status)
        {
            TaskProgress.IsIndeterminate = true;
            StatusText.Text = status;
            Say($"{status}", mood: "vigilant", context: "general");
        }

        private void ProgressDone(string status = "Terminé.")
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 100;
            StatusText.Text = status;
            Say(status, mood: "proud", context: "general");
        }

        private void ProgressReset()
        {
            TaskProgress.IsIndeterminate = false;
            TaskProgress.Value = 0;
            StatusText.Text = "Prêt.";
        }

        private void Say(string text, string mood = "neutral", string context = "general")
        {
            try
            {
                var vm = AvatarControl?.DataContext;
                vm?.GetType().GetMethod("SetMood")?.Invoke(vm, new object[] { mood, context });
            }
            catch { }
            AppendLine(text);
        }

        // ===== Maintenance presets
        private async void QuickMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Maintenance rapide…");
                var svc = new Virgil.Core.MaintenancePresetsService();

                // Étapes : 0/50/100 (temp + browsers)
                Progress(10, "Nettoyage TEMP…");
                await Task.Delay(150); // petite respiration UI
                var tempTxt = await Task.Run(() => {
                    var t = new CleaningService();
                    var size = t.GetTempFilesSize();
                    t.CleanTempFiles();
                    return $"[Temp] cleaned ~{size / (1024.0 * 1024):F1} MB";
                });
                AppendLine(tempTxt);
                Progress(50, "Navigateurs (caches)…");

                var browsers = new Virgil.Core.BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                AppendLine($"[Browsers] {rep}");

                ProgressDone("Maintenance rapide : terminé.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur maintenance rapide: {ex.Message}", "alert", "general");
            }
        }

        private async void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Étapes: 0 (start) / 25 (clean temp) / 45 (browsers) / 65 (extended) / 85 (winget) / 100 (WU install)
                Progress(0, "Maintenance complète : démarrage…");

                Progress(10, "Nettoyage TEMP…");
                await Task.Delay(100);
                await Task.Run(CleanTempWithProgressInternal);

                Progress(30, "Navigateurs (caches) …");
                var browsers = new Virgil.Core.BrowserCleaningService();
                var rep = await Task.Run(() => browsers.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                AppendLine($"[Browsers] {rep}");

                Progress(50, "Nettoyage étendu…");
                var ext = new Virgil.Core.ExtendedCleaningService();
                var exRep = await Task.Run(() => ext.AnalyzeAndClean());
                AppendLine($"[Extended] ~{exRep.BytesFound / (1024.0 * 1024):F1} MB → ~{exRep.BytesDeleted / (1024.0 * 1024):F1} MB");

                ProgressIndeterminate("MAJ apps/jeux (winget) …");
                var app = new Virgil.Core.ApplicationUpdateService();
                var wingetOut = await WingetWithRoughProgress(app);
                if (!string.IsNullOrWhiteSpace(wingetOut)) Append(wingetOut);

                ProgressIndeterminate("Windows Update (scan/download/install) …");
                var wu = new Virgil.Core.WindowsUpdateService();
                // pas de granularité fiable → on simule des étapes
                AppendLine("[WU] Scan…");
                await wu.StartScanAsync(); Progress(86, "WU: Download…");
                await wu.StartDownloadAsync(); Progress(93, "WU: Install…");
                var install = await wu.StartInstallAsync();
                if (!string.IsNullOrWhiteSpace(install)) Append(install);

                ProgressDone("Maintenance complète : terminé.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur maintenance complète: {ex.Message}", "alert", "general");
            }
        }

        // ===== Nettoyage TEMP (progression réelle par fichier)
        private async void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Nettoyage TEMP…");
                await Task.Run(CleanTempWithProgressInternal);
                ProgressDone("Nettoyage TEMP terminé.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur de nettoyage: {ex.Message}", "alert", "general");
            }
        }

        private void CleanTempWithProgressInternal()
        {
            var targets = new[]
            {
                Environment.ExpandEnvironmentVariables("%TEMP%"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            }.Where(Directory.Exists).ToList();

            // inventaire des fichiers
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
            {
                try { bytesFound += new FileInfo(f).Length; } catch { }
            }

            // suppression avec progression
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
                catch { /* ignore */ }

                done++;
                var p = Math.Floor(done / total * 100);
                Dispatcher.Invoke(() => Progress(p, $"Nettoyage TEMP… {p:0}%"));
            }

            // répertoires (sans granularité fine, mais rapide)
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

        // ===== Nettoyages complémentaires (progression “étapes”)
        private async void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Navigateurs: scan…");
                await Task.Delay(150);
                var svc = new Virgil.Core.BrowserCleaningService();
                var rep = await Task.Run(() => svc.AnalyzeAndClean(new BrowserCleaningOptions { Force = false }));
                Progress(100, "Navigateurs: terminé.");
                AppendLine($"Caches navigateurs détectés: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → supprimés: ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                Say("Caches navigateurs nettoyés.", "proud", "clean browsers");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur nettoyage navigateurs: {ex.Message}", "alert", "general");
            }
        }

        private async void CleanExtendedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(0, "Nettoyage étendu…");
                var ext = new Virgil.Core.ExtendedCleaningService();
                var rep = await Task.Run(() => ext.AnalyzeAndClean());
                Progress(100, "Nettoyage étendu terminé.");
                AppendLine($"Étendu: ~{rep.BytesFound / (1024.0 * 1024):F1} MB → ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                Say("Nettoyage étendu : ok.", "proud", "clean extended");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur Clean Extended: {ex.Message}", "alert", "general");
            }
        }

        // ===== Mises à jour
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("MAJ apps/jeux (winget)…");
                var app = new Virgil.Core.ApplicationUpdateService();
                var txt = await WingetWithRoughProgress(app);
                if (!string.IsNullOrWhiteSpace(txt)) Append(txt);
                ProgressDone("MAJ apps/jeux terminées.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur MAJ apps: {ex.Message}", "alert", "maj apps");
            }
        }

        private async Task<string> WingetWithRoughProgress(Virgil.Core.ApplicationUpdateService app)
        {
            // On ne peut pas brancher proprement un flux sur winget depuis ce service.
            // On simule donc une progression par paliers avec quelques micro-pauses.
            Progress(5, "Winget: inventaire…");
            await Task.Delay(300);
            var output = await app.UpgradeAllAsync(includeUnknown: true, silent: true);
            Progress(65, "Winget: installation…");
            await Task.Delay(200);
            Progress(92, "Winget: finalisation…");
            await Task.Delay(200);
            return output;
        }

        private async void DriverButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProgressIndeterminate("MAJ pilotes (best-effort) …");
                var drv = new Virgil.Core.DriverUpdateService();
                var output = await drv.UpgradeDriversAsync();
                if (!string.IsNullOrWhiteSpace(output)) Append(output);
                ProgressDone("Pilotes : vérification terminée.");
            }
            catch (Exception ex)
            {
                ProgressReset();
                Say($"Erreur pilotes: {ex.Message}", "alert", "maj pilotes");
            }
        }

        // ===== Windows Update (UsoClient) – progression en étapes
        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Scan…");
                var s = await new Virgil.Core.WindowsUpdateService().StartScanAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Scan demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Scan: {ex.Message}", "alert", "general"); }
        }

        private async void WindowsUpdateDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Download…");
                var s = await new Virgil.Core.WindowsUpdateService().StartDownloadAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Download demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Download: {ex.Message}", "alert", "general"); }
        }

        private async void WindowsUpdateInstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Install…");
                var s = await new Virgil.Core.WindowsUpdateService().StartInstallAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Install demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Install: {ex.Message}", "alert", "general"); }
        }

        private async void WindowsUpdateRestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Progress(10, "WU: Restart…");
                var s = await new Virgil.Core.WindowsUpdateService().RestartDeviceAsync();
                if (!string.IsNullOrWhiteSpace(s)) Append(s);
                ProgressDone("WU: Restart demandé.");
            }
            catch (Exception ex) { ProgressReset(); Say($"WU Restart: {ex.Message}", "alert", "general"); }
        }

        // ===== Monitoring
        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            _isMonitoring = !_isMonitoring;
            if (_isMonitoring && _monitoringService != null)
            {
                _monitoringService.Start();
                MonitorButton.Content = "Stop Monitoring";
                Say("Monitoring démarré.", "vigilant", "general");
            }
            else
            {
                _monitoringService?.Stop();
                MonitorButton.Content = "Start Monitoring";
                Say("Monitoring arrêté.", "neutral", "general");
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
            catch (Exception ex) { Say($"Erreur lecture températures: {ex.Message}", "alert", "general"); }
        }

        private string DecideMoodFromTemps(CoreServices.HardwareSnapshot s)
        {
            var cfg = _config.Current;
            if ((s.CpuTempC.HasValue && s.CpuTempC.Value >= cfg.CpuTempAlert) ||
                (s.GpuTempC.HasValue && s.GpuTempC.Value >= cfg.GpuTempAlert))
                return "alert";
            if ((s.CpuTempC.HasValue && s.CpuTempC.Value >= cfg.CpuTempWarn) ||
                (s.GpuTempC.HasValue && s.GpuTempC.Value >= cfg.GpuTempWarn))
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
                                   .Take(15)
                                   .ToList();

                Say("Top 15 processus par RAM :", "vigilant", "process list");
                foreach (var p in procs)
                {
                    long ws = 0; try { ws = p.WorkingSet64; } catch { }
                    AppendLine($"- {p.ProcessName} (PID {p.Id}) — {ws / (1024.0 * 1024):F1} MB");
                }
            }
            catch (Exception ex) { Say($"Erreur liste des processus: {ex.Message}", "alert", "general"); }
        }

        private void KillPidButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(KillPidBox.Text, out var pid))
                { Say("PID invalide.", "alert", "general"); return; }

                try { Process.GetProcessById(pid).Kill(true); Say($"Processus {pid} terminé.", "proud", "general"); }
                catch { Say($"Impossible de terminer {pid} (droits/admin ?).", "alert", "general"); }
            }
            catch (Exception ex) { Say($"Erreur kill: {ex.Message}", "alert", "general"); }
        }

        // ===== Services
        private void ListServicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sm = new CoreServices.ServiceManager();
                var list = sm.ListAll().Take(30).ToList();
                Say("Services (30 premiers) :", "vigilant", "general");
                foreach (var s in list) AppendLine($"- {s.DisplayName} ({s.Name}) — {s.Status}");
            }
            catch (Exception ex) { Say($"Erreur list services: {ex.Message}", "alert", "general"); }
        }

        private void RestartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { Say("Nom de service manquant.", "alert", "general"); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Restart(name);
                Say(ok ? $"Service {name} redémarré." : $"Échec restart {name}.", ok ? "proud" : "alert", "general");
            }
            catch (Exception ex) { Say($"Erreur restart: {ex.Message}", "alert", "general"); }
        }

        private void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { Say("Nom de service manquant.", "alert", "general"); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Start(name);
                Say(ok ? $"Service {name} démarré." : $"Échec start {name}.", ok ? "proud" : "alert", "general");
            }
            catch (Exception ex) { Say($"Erreur start: {ex.Message}", "alert", "general"); }
        }

        private void StopServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { Say("Nom de service manquant.", "alert", "general"); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Stop(name);
                Say(ok ? $"Service {name} arrêté." : $"Échec stop {name}.", ok ? "proud" : "alert", "general");
            }
            catch (Exception ex) { Say($"Erreur stop: {ex.Message}", "alert", "general"); }
        }

        // ===== Monitoring callback
        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            if (_monitoringService == null) return;
            var m = _monitoringService.LatestMetrics;

            Dispatcher.Invoke(() =>
            {
                AppendLine($"CPU: {m.CpuUsage:F1}%  MEM: {m.MemoryUsage:F1}%");
            });

            var cfg = _config.Current;
            if (m.CpuUsage >= cfg.CpuAlert || m.MemoryUsage >= cfg.MemAlert)      Say("Charge élevée.", "alert", "charge élevée");
            else if (m.CpuUsage >= cfg.CpuWarn || m.MemoryUsage >= cfg.MemWarn)   Say("Charge modérée.", "vigilant", "charge modérée");
            else                                                                  Say("Charge normale.", "neutral", "charge normale");
        }

        // ===== Config
        private void ShowConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var c = _config.Current;
            Say($"Config: CPU warn/alert {c.CpuWarn}/{c.CpuAlert} ; MEM warn/alert {c.MemWarn}/{c.MemAlert} ; CPU° {c.CpuTempWarn}/{c.CpuTempAlert} ; GPU° {c.GpuTempWarn}/{c.GpuTempAlert}",
                "neutral", "general");
        }

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _config.SaveUser();
                Say("Config utilisateur sauvegardée (%AppData%\\Virgil\\user.json).", "proud", "general");
            }
            catch (Exception ex) { Say($"Erreur save config: {ex.Message}", "alert", "general"); }
        }
    }
}
