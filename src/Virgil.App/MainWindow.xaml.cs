#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Virgil.Core;


namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // Monitoring (simple timer via PerformanceCounter dans Virgil.Core si dispo,
        // sinon on se contente d'un "stub" ici pour ne pas casser le build).
        private readonly Virgil.Core.MonitoringService? _monitoringService;
        private bool _isMonitoring;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                _monitoringService = new Virgil.Core.MonitoringService();
                _monitoringService.MetricsUpdated += OnMetricsUpdated;
            }
            catch
            {
                // Si le service n'existe pas ou plante, on laisse l'UI vivante.
                _monitoringService = null;
            }

            // DataContext pour l’avatar si présent
            try
            {
                var vmType = Type.GetType("Virgil.App.Controls.VirgilAvatarViewModel, Virgil.App");
                if (vmType != null && AvatarControl != null)
                {
                    var vm = Activator.CreateInstance(vmType);
                    AvatarControl.DataContext = vm;
                    // message d’accueil
                    AppendLine("Bonjour 👋 Virgil est prêt.");
                    vmType.GetMethod("SetMood")?.Invoke(vm, new object[] { "neutral", "Démarrage" });
                }
            }
            catch { /* pas bloquant */ }
        }

        // ---- Helpers UI -------------------------------------------------------

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

        // ---- Actions principales ---------------------------------------------

        private void CleanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendLine("Scan des fichiers temporaires...");
                var targets = new[]
                {
                    Environment.ExpandEnvironmentVariables("%TEMP%"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Temp"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Temp"),
                };

                long totalBytes = 0;
                foreach (var t in targets.Where(Directory.Exists))
                {
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    {
                        try { totalBytes += new FileInfo(f).Length; } catch { }
                    }
                }
                AppendLine($"Trouvé ~{totalBytes / (1024.0 * 1024):F1} MB de fichiers temporaires.");
                int deleted = 0;

                foreach (var t in targets.Where(Directory.Exists))
                {
                    // On tente la suppression, best-effort
                    foreach (var f in Directory.EnumerateFiles(t, "*", SearchOption.AllDirectories))
                    { try { File.Delete(f); deleted++; } catch { } }
                    foreach (var d in Directory.EnumerateDirectories(t, "*", SearchOption.AllDirectories))
                    { try { Directory.Delete(d, true); } catch { } }
                }
                AppendLine($"Nettoyage terminé. Fichiers supprimés: {deleted} (best-effort).");
                SetMoodSafe("proud", "Nettoyage terminé");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur de nettoyage: {ex.Message}");
                SetMoodSafe("alert", "Erreur nettoyage");
            }
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("Recherche des mises à jour (apps/jeux) via winget…");
            var output = await RunProcessAsync("winget", "upgrade --all --include-unknown --silent");
            Append(output);
            AppendLine("Mises à jour (apps/jeux) terminées.");
            SetMoodSafe("vigilant", "MAJ terminées");
        }

        private async void DriverButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLine("Recherche des mises à jour de pilotes (winget)…");
            // NB: winget couvre quelques pilotes; pour NVIDIA/AMD/Intel, outils dédiés recommandés.
            var output = await RunProcessAsync("winget", "upgrade --all --include-unknown --silent");
            Append(output);
            AppendLine("Vérification pilotes terminée (voir détails ci-dessus).");
            SetMoodSafe("neutral", "Pilotes vérifiés");
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

        // ---- Handlers AJOUTÉS (répara le commit vide) ------------------------

        private void StartupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Bascule simple HKCU\...\Run\Virgil vers l’agent
                using var rk = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                var current = rk.GetValue("Virgil") as string;
                if (string.IsNullOrWhiteSpace(current))
                {
                    var path = "\"%ProgramFiles%\\Virgil\\Virgil.Agent\\Virgil.Agent.exe\"";
                    rk.SetValue("Virgil", path);
                    AppendLine("Démarrage automatique ACTIVÉ (HKCU\\...\\Run\\Virgil).");
                    SetMoodSafe("proud", "Startup activé");
                }
                else
                {
                    rk.DeleteValue("Virgil", false);
                    AppendLine("Démarrage automatique DÉSACTIVÉ.");
                    SetMoodSafe("neutral", "Startup désactivé");
                }
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur démarrage automatique: {ex.Message}");
                SetMoodSafe("alert", "Erreur startup");
            }
        }

        private void ProcessesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var procs = Process.GetProcesses()
                                   .OrderByDescending(p => SafeWs(p))
                                   .Take(15)
                                   .ToList();

                AppendLine($"Top 15 processus par RAM (Working Set) :");
                foreach (var p in procs)
                {
                    AppendLine($"- {p.ProcessName} (PID {p.Id}) — {SafeWs(p) / (1024.0 * 1024):F1} MB");
                }
                AppendLine("Astuce: pour fermer un processus bloqué, utilisez le Gestionnaire des tâches.");
                SetMoodSafe("vigilant", "Processus listés");
            }
            catch (Exception ex)
            {
                AppendLine($"Erreur liste des processus: {ex.Message}");
                SetMoodSafe("alert", "Erreur processus");
            }
        }

        private async void WindowsUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // Scan Windows Update via usoclient (peut varier selon versions Windows)
            AppendLine("Windows Update: recherche des mises à jour (scan)...");
            var result = await RunProcessAsync("powershell",
                "Get-WindowsUpdate -ErrorAction SilentlyContinue",
                runAsShell: false);
            if (string.IsNullOrWhiteSpace(result))
            {
                // Fallback simple
                await RunProcessAsync("cmd.exe", "/c UsoClient StartScan", runAsShell: false);
                AppendLine("Scan lancé via UsoClient (retour muet).");
            }
            else
            {
                Append(result);
            }
            SetMoodSafe("neutral", "WU scan");
        }

        // ---- Monitoring callback ---------------------------------------------

        private void OnMetricsUpdated(object? sender, EventArgs e)
        {
            if (_monitoringService == null) return;
            var m = _monitoringService.LatestMetrics;

            Dispatcher.Invoke(() =>
            {
                AppendLine($"CPU: {m.CpuUsage:F1}%  MEM: {m.MemoryUsage:F1}%");
            });

            // Changer l’humeur selon seuils
            if (m.CpuUsage >= 85 || m.MemoryUsage >= 85)
                SetMoodSafe("alert", "Charge élevée");
            else if (m.CpuUsage >= 60 || m.MemoryUsage >= 70)
                SetMoodSafe("vigilant", "Charge modérée");
            else
                SetMoodSafe("neutral", "Charge normale");
        }

        // ---- Utils ------------------------------------------------------------

        private static long SafeWs(Process p)
        {
            try { return p.WorkingSet64; } catch { return 0; }
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
                var mi = vm.GetType().GetMethod("SetMood");
                mi?.Invoke(vm, new object[] { mood, source });
            }
            catch { /* non bloquant */ }
        }
    }
}
