#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace Virgil.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Service de monitoring (depuis Virgil.Core si présent)
        private readonly Virgil.Core.MonitoringService? _monitoringService;
        private bool _isMonitoring;

        public MainWindow()
        {
            InitializeComponent();

            // Monitoring (optionnel si le service existe)
            try
            {
                _monitoringService = new Virgil.Core.MonitoringService();
                _monitoringService.MetricsUpdated += OnMetricsUpdated;
            }
            catch
            {
                _monitoringService = null;
            }

            // DataContext pour l’avatar si ViewModel présent (réflexion, non bloquant)
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

        // ------------------ Helpers UI ------------------

        private void Append(string text)
        {
            OutputBox.AppendText(text);
            OutputBox.ScrollToEnd();
        }

        private void AppendLine(string line)
        {
            OutputBox.AppendText($"[{DateTime.Now:T}] {line}\n");
            OutputBox.ScrollToEnd();
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
                    p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                }
                p.Start();
                if (!runAsShell)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }
                await Task.Run(() => p.WaitForExit());
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[process error] {ex.Message}\n";
            }
        }

        private void SetMoodSafe(string mood, string source)
        {
            try
            {
                var vm = AvatarControl?.DataContext;
                if (vm == null) return;
                vm.GetType().GetMethod("SetMood")?.Invoke(vm, new object[] { mood, source });
            }
            catch { /* ignore */ }
        }

        // ------------------ Actions Maintenance ------------------

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

                long totalBytes = 0;
                foreach (var t in targets.Where(Directory.Exists))
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    {
                        try { totalBytes += new FileInfo(f).Length; } catch { }
                    }
                }
                AppendLine($"Trouvé ~{totalBytes / (1024.0 * 1024):F1} MB.");
                int deleted = 0;

                foreach (var t in targets.Where(Directory.Exists))
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    { try { File.Delete(f); deleted++; } catch { } }
                    foreach (var d in Directory.EnumerateDirectories(t, "*", SearchOption.AllDirectories))
                    { try { Directory.Delete(d, true); } catch { } }
                }
                AppendLine($"Nettoyage terminé. Fichiers supprimés: {deleted}.");
                SetMoodSafe("proud", "Clean temp");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur de nettoyage: {ex.Message}");
                SetMoodSafe("alert", "Clean temp error");
            }
        }

        private void CleanBrowsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Nettoyage simple multi-navigateurs sans dépendre d'un service externe
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                string[] chromiumRoots =
                {
                    Path.Combine(local, "Google\\Chrome\\User Data"),
                    Path.Combine(local, "Microsoft\\Edge\\User Data"),
                    Path.Combine(local, "BraveSoftware\\Brave-Browser\\User Data"),
                    Path.Combine(local, "Vivaldi\\User Data"),
                    Path.Combine(local, "Opera Software\\Opera Stable"),
                    Path.Combine(local, "Opera Software\\Opera GX Stable"),
                    Path.Combine(local, "Chromium\\User Data"),
                    Path.Combine(local, "Yandex\\YandexBrowser\\User Data")
                };
                string[] chromiumPatterns =
                {
                    "Cache","Code Cache","GPUCache","GrShaderCache","ShaderCache",
                    "Media Cache","Session Storage","Service Worker\\CacheStorage",
                    "IndexedDB","Local Storage"
                };
                string[] firefoxProfilesRoot = { Path.Combine(roaming, "Mozilla\\Firefox\\Profiles") };
                string[] firefoxPatterns = { "cache2","startupCache","jumpListCache","shader-cache" };

                // Vérifier si un navigateur tourne
                string[] procNames = { "chrome","msedge","brave","vivaldi","opera","opera_gx","chromium","yandex","firefox","waterfox","librewolf" };
                bool anyBrowser = Process.GetProcesses().Any(p =>
                {
                    try { return procNames.Contains(Path.GetFileNameWithoutExtension(p.ProcessName).ToLowerInvariant()); }
                    catch { return false; }
                });

                if (anyBrowser)
                {
                    AppendLine("Un navigateur est en cours d’exécution. Fermez-le(s) pour un nettoyage complet.");
                    return;
                }

                long found = 0, deleted = 0;

                // Chromium-like
                foreach (var root in chromiumRoots.Where(Directory.Exists))
                {
                    // profils: Default, Profile X, root, etc.
                    var candidates = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                                              .Concat(new[] { root })
                                              .Distinct();
                    foreach (var prof in candidates)
                    {
                        foreach (var pat in chromiumPatterns)
                        {
                            var path = Path.Combine(prof, pat);
                            if (!Directory.Exists(path)) continue;

                            try
                            {
                                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                                { try { found += new FileInfo(f).Length; } catch { } }

                                // suppression best-effort
                                try { Directory.Delete(path, true); deleted += found; }
                                catch
                                {
                                    try
                                    {
                                        foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                                        { try { File.Delete(f); } catch { } }
                                        foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                                        { try { Directory.Delete(d, true); } catch { } }
                                        try { Directory.Delete(path, false); } catch { }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    }
                }

                // Firefox/Gecko
                foreach (var basePath in firefoxProfilesRoot.Where(Directory.Exists))
                {
                    var profiles = Directory.EnumerateDirectories(basePath, "*.default*", SearchOption.TopDirectoryOnly);
                    foreach (var prof in profiles)
                    {
                        foreach (var pat in firefoxPatterns)
                        {
                            var path = Path.Combine(prof, pat);
                            if (!Directory.Exists(path)) continue;

                            try
                            {
                                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                                { try { found += new FileInfo(f).Length; } catch { } }

                                try { Directory.Delete(path, true); deleted += found; }
                                catch
                                {
                                    try
                                    {
                                        foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                                        { try { File.Delete(f); } catch { } }
                                        foreach (var d in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                                        { try { Directory.Delete(d, true); } catch { } }
                                        try { Directory.Delete(path, false); } catch { }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    }
                }

                AppendLine($"Caches navigateurs détectés: ~{found / (1024.0 * 1024):F1} MB");
                AppendLine($"Caches navigateurs supprimés: ~{deleted / (1024.0 * 1024):F1} MB");
                SetMoodSafe(deleted > 0 ? "proud" : "neutral", "Clean browsers");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur nettoyage navigateurs: {ex.Message}");
                SetMoodSafe("alert", "Clean browsers error");
            }
        }

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

        // ------------------ Démarrage & Processus ------------------

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
                                   .OrderByDescending(p =>
                                   {
                                       try { return p.WorkingSet64; } catch { return 0L; }
                                   })
                                   .Take(15)
                                   .ToList();

                AppendLine("Top 15 processus par RAM (Working Set) :");
                foreach (var p in procs)
                {
                    long ws = 0;
                    try { ws = p.WorkingSet64; } catch { }
                    AppendLine($"- {p.ProcessName} (PID {p.Id}) — {ws / (1024.0 * 1024):F1} MB");
                }
                AppendLine("Astuce: pour fermer un processus bloqué, utilisez le Gestionnaire des tâches ou le champ Kill PID.");
                SetMoodSafe("vigilant", "Processus listés");
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
                try
                {
                    Process.GetProcessById(pid).Kill(true);
                    AppendLine($"Processus {pid} terminé.");
                    SetMoodSafe("proud", "Kill OK");
                }
                catch
                {
                    AppendLine($"Impossible de terminer {pid} (droits/admin ?).");
                    SetMoodSafe("alert", "Kill KO");
                }
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur kill: {ex.Message}");
                SetMoodSafe("alert", "Kill error");
            }
        }

        // ------------------ Windows Update (direct UsoClient) ------------------

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
            // Chemin absolu préféré (évite PATH)
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
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
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

        // ------------------ Monitoring callback ------------------

        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            if (_monitoringService == null) return;
            var m = _monitoringService.LatestMetrics;

            Dispatcher.Invoke(() =>
            {
                AppendLine($"CPU: {m.CpuUsage:F1}%  MEM: {m.MemoryUsage:F1}%");
            });

            if (m.CpuUsage >= 85 || m.MemoryUsage >= 85)
                SetMoodSafe("alert", "Charge élevée");
            else if (m.CpuUsage >= 60 || m.MemoryUsage >= 70)
                SetMoodSafe("vigilant", "Charge modérée");
            else
                SetMoodSafe("neutral", "Charge normale");
        }
    }
}
