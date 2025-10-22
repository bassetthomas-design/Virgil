#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Serilog;
using Serilog.Events;
using Virgil.Core; // pour MonitoringService (basique)
using CoreServices = Virgil.Core.Services; // <<< alias vers les services avancés

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

            // Services de base
            _config = new CoreServices.ConfigService();
            CoreServices.LoggingService.Init(LogEventLevel.Information);

            try
            {
                _monitoringService = new Virgil.Core.MonitoringService();
                _monitoringService.MetricsUpdated += OnMetricsUpdated;
            }
            catch { _monitoringService = null; }

            // Avatar VM si présent
            try
            {
                var vmType = Type.GetType("Virgil.App.Controls.VirgilAvatarViewModel, Virgil.App");
                if (vmType != null && AvatarControl != null)
                {
                    var vm = Activator.CreateInstance(vmType);
                    AvatarControl.DataContext = vm;
                    AppendLine("Bonjour 👋 Virgil est prêt.");
                    vmType.GetMethod("SetMood")?.Invoke(vm, new object[] { "neutral", "Startup" });
                }
            }
            catch { /* ignore */ }
        }

        // ---------- Helpers UI ----------
        private void Append(string text) { OutputBox.AppendText(text); OutputBox.ScrollToEnd(); }
        private void AppendLine(string line) { OutputBox.AppendText($"[{DateTime.Now:T}] {line}\n"); OutputBox.ScrollToEnd(); }
        private void SetMoodSafe(string mood, string source)
        {
            try
            {
                var vm = AvatarControl?.DataContext;
                vm?.GetType().GetMethod("SetMood")?.Invoke(vm, new object[] { mood, source });
            } catch { }
        }

        private async Task<string> RunProcessAsync(string fileName, string args, bool runAsShell = false)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = runAsShell,
                    RedirectStandardOutput = !runAsShell,
                    RedirectStandardError = !runAsShell,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var sb = new StringBuilder();
                using var p = new Process { StartInfo = psi };
                if (!runAsShell)
                {
                    p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                    p.ErrorDataReceived +=  (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                }
                p.Start();
                if (!runAsShell) { p.BeginOutputReadLine(); p.BeginErrorReadLine(); }
                await Task.Run(() => p.WaitForExit());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[process error] {ex.Message}\n";
            }
        }

        // ---------- Presets ----------
        private async void QuickMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLine("Maintenance rapide en cours…");
                var svc = new CoreServices.MaintenancePresetsService();
                var report = await svc.QuickCleanAsync(forceBrowser: false);
                Append(report);
                AppendLine("Maintenance rapide terminée.");
                SetMoodSafe("proud", "Quick maintenance");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur maintenance rapide: {ex.Message}");
                SetMoodSafe("alert", "Quick maintenance error");
            }
        }

        private async void FullMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLine("Maintenance complète en cours (clean + maj + WU)…");
                var svc = new CoreServices.MaintenancePresetsService();
                var report = await svc.FullMaintenanceAsync(forceBrowser: false, windowsRestart: false);
                Append(report);
                AppendLine("Maintenance complète terminée.");
                SetMoodSafe("proud", "Full maintenance");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur maintenance complète: {ex.Message}");
                SetMoodSafe("alert", "Full maintenance error");
            }
        }

        // ---------- Nettoyage ----------
        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLine("Scan des fichiers temporaires…");
                var targets = new[]
                {
                    Environment.ExpandEnvironmentVariables("%TEMP%"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
                };

                long found = 0; int deleted = 0;
                foreach (var t in targets.Where(Directory.Exists))
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    { try { found += new FileInfo(f).Length; } catch { } }
                }
                foreach (var t in targets.Where(Directory.Exists))
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    { try { File.Delete(f); deleted++; } catch { } }
                    foreach (var d in Directory.EnumerateDirectories(t, "*", SearchOption.AllDirectories))
                    { try { Directory.Delete(d, true); } catch { } }
                }
                AppendLine($"Temp détecté ~{found / (1024.0 * 1024):F1} MB — supprimé {deleted} fichiers.");
                SetMoodSafe("proud", "Clean temp");
                CoreServices.LoggingService.SafeInfo("Temp cleaned {Deleted} files (~{MB} MB)", deleted, found / (1024.0 * 1024));
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur de nettoyage: {ex.Message}");
                SetMoodSafe("alert", "Clean temp error");
                CoreServices.LoggingService.SafeError(ex, "Clean temp error");
            }
        }

        private void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new CoreServices.BrowserCleaningService();
                if (svc.IsAnyBrowserRunning())
                {
                    AppendLine("Un navigateur est en cours d’exécution. Fermez-le(s) pour un nettoyage complet.");
                    return;
                }
                var rep = svc.AnalyzeAndClean(new CoreServices.BrowserCleaningOptions { Force = false });
                AppendLine($"Caches navigateurs détectés: ~{rep.BytesFound / (1024.0 * 1024):F1} MB");
                AppendLine($"Caches navigateurs supprimés: ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                SetMoodSafe(rep.BytesDeleted > 0 ? "proud" : "neutral", "Clean browsers");
                CoreServices.LoggingService.SafeInfo("Browser caches cleaned (~{MB} MB)", rep.BytesDeleted / (1024.0 * 1024));
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur nettoyage navigateurs: {ex.Message}");
                SetMoodSafe("alert", "Clean browsers error");
                CoreServices.LoggingService.SafeError(ex, "Clean browsers error");
            }
        }

        private void CleanExtendedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var svc = new CoreServices.ExtendedCleaningService();
                var rep = svc.AnalyzeAndClean();
                AppendLine($"Nettoyage étendu: détecté ~{rep.BytesFound / (1024.0 * 1024):F1} MB — supprimé ~{rep.BytesDeleted / (1024.0 * 1024):F1} MB");
                SetMoodSafe(rep.BytesDeleted > 0 ? "proud" : "neutral", "Clean extended");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur Clean Extended: {ex.Message}");
                SetMoodSafe("alert", "Clean extended error");
            }
        }

        // ---------- Mises à jour ----------
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("Recherche des mises à jour (apps/jeux) via winget…");
            var output = await RunProcessAsync("winget", "upgrade --all --include-unknown --silent");
            if (!string.IsNullOrWhiteSpace(output)) Append(output);
            AppendLine("Mises à jour (apps/jeux) terminées.");
            SetMoodSafe("vigilant", "MAJ apps");
        }

        private async void DriverButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("Recherche des mises à jour de pilotes (via winget)…");
            var output = await RunProcessAsync("winget", "upgrade --all --include-unknown --silent");
            if (!string.IsNullOrWhiteSpace(output)) Append(output);
            AppendLine("Pilotes: vérification demandée (voir détails).");
            SetMoodSafe("neutral", "MAJ pilotes");
        }

        // ---------- Surveillance ----------
        private void MonitorButton_Click(object sender, RoutedEventArgs e)
        {
            _isMonitoring = !_isMonitoring;
            if (_isMonitoring && _monitoringService != null)
            {
                _monitoringService.Start();
                MonitorButton.Content = "Stop Monitoring";
                AppendLine("Monitoring démarré.");
                SetMoodSafe("vigilant", "Surveillance active");
            }
            else
            {
                _monitoringService?.Stop();
                MonitorButton.Content = "Start Monitoring";
                AppendLine("Monitoring arrêté.");
                SetMoodSafe("neutral", "Surveillance arrêtée");
            }
        }

        private void ReadTempsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var adv = new CoreServices.AdvancedMonitoringService();
                var s = adv.Read();
                AppendLine($"Températures → CPU: {(s.CpuTempC?.ToString("F0") ?? "?")}°C | GPU: {(s.GpuTempC?.ToString("F0") ?? "?")}°C | Disque: {(s.DiskTempC?.ToString("F0") ?? "?")}°C");
                SetMoodSafe( DecideMoodFromTemps(s), "Temps read" );
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur lecture températures: {ex.Message}");
                SetMoodSafe("alert", "Temps error");
            }
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

        // ---------- Démarrage & Processus ----------
        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var rk = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                var current = rk.GetValue("Virgil") as string;
                if (string.IsNullOrWhiteSpace(current))
                {
                    var path = "\"%ProgramFiles%\\Virgil\\Virgil.Agent\\Virgil.Agent.exe\"";
                    rk.SetValue("Virgil", path);
                    AppendLine("Démarrage automatique ACTIVÉ (HKCU\\...\\Run\\Virgil).");
                    SetMoodSafe("proud", "Startup ON");
                }
                else
                {
                    rk.DeleteValue("Virgil", false);
                    AppendLine("Démarrage automatique DÉSACTIVÉ.");
                    SetMoodSafe("neutral", "Startup OFF");
                }
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur démarrage automatique: {ex.Message}");
                SetMoodSafe("alert", "Startup error");
            }
        }

        private void ProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var procs = Process.GetProcesses()
                                   .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0L; } })
                                   .Take(15)
                                   .ToList();

                AppendLine("Top 15 processus par RAM :");
                foreach (var p in procs)
                {
                    long ws = 0; try { ws = p.WorkingSet64; } catch { }
                    AppendLine($"- {p.ProcessName} (PID {p.Id}) — {ws / (1024.0 * 1024):F1} MB");
                }
                SetMoodSafe("vigilant", "Process list");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur liste des processus: {ex.Message}");
                SetMoodSafe("alert", "Process error");
            }
        }

        private void KillPidButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(KillPidBox.Text, out var pid))
                {
                    AppendLine("PID invalide.");
                    return;
                }
                try { Process.GetProcessById(pid).Kill(true); AppendLine($"Processus {pid} terminé."); SetMoodSafe("proud", "Kill OK"); }
                catch { AppendLine($"Impossible de terminer {pid} (droits/admin ?)."); SetMoodSafe("alert", "Kill KO"); }
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur kill: {ex.Message}");
                SetMoodSafe("alert", "Kill error");
            }
        }

        // ---------- Windows Update (UsoClient) ----------
        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("WU: Scan…");
            var s = await RunUsoAsync("StartScan");
            if (!string.IsNullOrWhiteSpace(s)) Append(s);
            AppendLine("WU: Scan demandé.");
        }
        private async void WindowsUpdateDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("WU: Download…");
            var s = await RunUsoAsync("StartDownload");
            if (!string.IsNullOrWhiteSpace(s)) Append(s);
            AppendLine("WU: Download demandé.");
        }
        private async void WindowsUpdateInstallButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("WU: Install…");
            var s = await RunUsoAsync("StartInstall");
            if (!string.IsNullOrWhiteSpace(s)) Append(s);
            AppendLine("WU: Install demandé (peut être silencieux).");
        }
        private async void WindowsUpdateRestartButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("WU: Restart (si requis)…");
            var s = await RunUsoAsync("RestartDevice");
            if (!string.IsNullOrWhiteSpace(s)) Append(s);
            AppendLine("WU: Restart demandé.");
        }

        private static async Task<string> RunUsoAsync(string arg)
        {
            string uso = Path.Combine(Environment.SystemDirectory, "UsoClient.exe");
            if (!File.Exists(uso)) uso = "UsoClient.exe";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = uso,
                    Arguments = arg,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = new Process { StartInfo = psi };
                var sb = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.ErrorDataReceived  += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await Task.Run(() => p.WaitForExit());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[UsoClient error] {ex.Message}\n";
            }
        }

        // ---------- Services Windows ----------
        private void ListServicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sm = new CoreServices.ServiceManager();
                var list = sm.ListAll().Take(30).ToList(); // limiter l’affichage
                AppendLine("Services (30 premiers triés par nom) :");
                foreach (var s in list)
                    AppendLine($"- {s.DisplayName} ({s.Name}) — {s.Status}");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur list services: {ex.Message}");
            }
        }

        private void RestartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { AppendLine("Nom de service manquant."); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Restart(name);
                AppendLine(ok ? $"Service {name} redémarré." : $"Échec restart {name}.");
            }
            catch (Exception ex) { AppendLine($"Erreur restart: {ex.Message}"); }
        }

        private void StartServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { AppendLine("Nom de service manquant."); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Start(name);
                AppendLine(ok ? $"Service {name} démarré." : $"Échec start {name}.");
            }
            catch (Exception ex) { AppendLine($"Erreur start: {ex.Message}"); }
        }

        private void StopServiceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var name = SvcNameBox.Text?.Trim();
                if (string.IsNullOrEmpty(name)) { AppendLine("Nom de service manquant."); return; }
                var sm = new CoreServices.ServiceManager();
                var ok = sm.Stop(name);
                AppendLine(ok ? $"Service {name} arrêté." : $"Échec stop {name}.");
            }
            catch (Exception ex) { AppendLine($"Erreur stop: {ex.Message}"); }
        }

        // ---------- Monitoring callback ----------
        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            if (_monitoringService == null) return;
            var m = _monitoringService.LatestMetrics;

            Dispatcher.Invoke(() =>
            {
                AppendLine($"CPU: {m.CpuUsage:F1}%  MEM: {m.MemoryUsage:F1}%");
            });

            var cfg = _config.Current;
            if (m.CpuUsage >= cfg.CpuAlert || m.MemoryUsage >= cfg.MemAlert)      SetMoodSafe("alert", "Charge élevée");
            else if (m.CpuUsage >= cfg.CpuWarn || m.MemoryUsage >= cfg.MemWarn)   SetMoodSafe("vigilant", "Charge modérée");
            else                                                                   SetMoodSafe("neutral", "Charge normale");
        }

        // ---------- Config ----------
        private void ShowConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var c = _config.Current;
            AppendLine($"Config: CPU warn/alert {c.CpuWarn}/{c.CpuAlert} ; MEM warn/alert {c.MemWarn}/{c.MemAlert} ; CPU° {c.CpuTempWarn}/{c.CpuTempAlert} ; GPU° {c.GpuTempWarn}/{c.GpuTempAlert}");
        }

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _config.SaveUser();
                AppendLine("Config utilisateur sauvegardée (%AppData%\\Virgil\\user.json).");
            }
            catch (Exception ex) { AppendLine($"Erreur save config: {ex.Message}"); }
        }
    }
}
