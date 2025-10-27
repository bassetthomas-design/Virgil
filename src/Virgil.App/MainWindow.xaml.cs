using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Virgil.App.Controls;
using CfgService = Virgil.Core.Services.ConfigService;   // service de config côté Services
using CfgModel   = Virgil.Core.Config.VirgilConfig;      // modèle de config côté Config
using Virgil.Core.Services;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // Timers
        private readonly DispatcherTimer _clockTimer  = new();
        private readonly DispatcherTimer _survTimer   = new();   // monitoring pulse
        private readonly DispatcherTimer _banterTimer = new();   // punchlines 1–6 min

        // Services (Core)
        private readonly CfgService _config                     = new();
        private readonly MaintenancePresetsService _presets     = new();
        private readonly Virgil.Core.Services.CleaningService _cleaning = new();
        private readonly BrowserCleaningService _browsers       = new();
        private readonly ExtendedCleaningService _extended      = new();
        private readonly ApplicationUpdateService _apps         = new();
        private readonly DriverUpdateService _drivers           = new();
        private readonly WindowsUpdateService _wu               = new();
        private readonly DefenderUpdateService _def             = new();
        private readonly AdvancedMonitoringService _monitor     = new();

        // Chat
        private readonly ObservableCollection<ChatItem> _chat = new();

        // Config fusionnée (machine + user)
        private CfgModel _cfg = new CfgModel();

        public MainWindow()
        {
            InitializeComponent();

            // Chat binding
            ChatList.ItemsSource = _chat;

            // Config (fusion machine + user) — safe
            _cfg = LoadConfigSafe();

            ThresholdsText.Text =
                $"CPU warn/alert: {GetCpuWarn()}% / {GetCpuAlert()}%{Environment.NewLine}" +
                $"RAM warn/alert: {GetRamWarn()}% / {GetRamAlert()}%{Environment.NewLine}" +
                $"Temp CPU warn/alert: {GetTempWarn(\"Cpu\")}°C / {GetTempAlert(\"Cpu\")}°C{Environment.NewLine}" +
                $"Temp GPU warn/alert: {GetTempWarn(\"Gpu\")}°C / {GetTempAlert(\"Gpu\")}°C{Environment.NewLine}" +
                $"Temp Disk warn/alert: {GetTempWarn(\"Disk\")}°C / {GetTempAlert(\"Disk\")}°C";

            InitTimers();
            Say("Salut, je suis Virgil 👋", Mood.Neutral);
            SetAvatarMood("neutral");
        }

        // =========================
        //   SAFE helpers (config)
        // =========================
        private CfgModel LoadConfigSafe()
        {
            try
            {
                // Essaye .LoadMerged()
                var m = typeof(CfgService).GetMethod("LoadMerged", BindingFlags.Instance | BindingFlags.Public);
                if (m != null)
                {
                    var v = m.Invoke(_config, null);
                    if (v is CfgModel ok1) return ok1;
                }
                // Essaye .Load()
                m = typeof(CfgService).GetMethod("Load", BindingFlags.Instance | BindingFlags.Public);
                if (m != null)
                {
                    var v = m.Invoke(_config, null);
                    if (v is CfgModel ok2) return ok2;
                }
            }
            catch { /* ignore */ }

            // Fallback: lecture directe %ProgramData% puis %AppData% (user override)
            try
            {
                var machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "config.json");
                var user    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)     , "Virgil", "user.json");
                CfgModel cfg = new();

                if (File.Exists(machine))
                {
                    var c = JsonSerializer.Deserialize<CfgModel>(File.ReadAllText(machine));
                    if (c != null) cfg = c;
                }
                if (File.Exists(user))
                {
                    var u = JsonSerializer.Deserialize<CfgModel>(File.ReadAllText(user));
                    if (u != null) cfg = MergeUserOnMachine(cfg, u);
                }
                return cfg;
            }
            catch { }

            return new CfgModel(); // défaut
        }

        private object GetConfigLocationsSafe()
        {
            try
            {
                var m = typeof(CfgService).GetMethod("GetConfigLocations", BindingFlags.Instance | BindingFlags.Public);
                if (m != null)
                {
                    return m.Invoke(_config, null) ?? new { Machine = "-", User = "-" };
                }
            }
            catch { }
            var machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "config.json");
            var user    = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)     , "Virgil", "user.json");
            return new { Machine = machine, User = user };
        }

        private static CfgModel MergeUserOnMachine(CfgModel machine, CfgModel user)
        {
            // Merge ultra-sécurisée : si user a une valeur non-default, on remplace
            try
            {
                if (user?.Thresholds != null)
                {
                    machine.Thresholds ??= new();
                    // CPU
                    if (user.Thresholds.Cpu != null)
                    {
                        machine.Thresholds.Cpu ??= new();
                        if (user.Thresholds.Cpu.Warn > 0)  machine.Thresholds.Cpu.Warn  = user.Thresholds.Cpu.Warn;
                        if (user.Thresholds.Cpu.Alert > 0) machine.Thresholds.Cpu.Alert = user.Thresholds.Cpu.Alert;
                    }
                    // RAM
                    if (user.Thresholds.Ram != null)
                    {
                        machine.Thresholds.Ram ??= new();
                        if (user.Thresholds.Ram.Warn > 0)  machine.Thresholds.Ram.Warn  = user.Thresholds.Ram.Warn;
                        if (user.Thresholds.Ram.Alert > 0) machine.Thresholds.Ram.Alert = user.Thresholds.Ram.Alert;
                    }
                    // Temps
                    if (user.Thresholds.Temps != null)
                    {
                        machine.Thresholds.Temps ??= new();
                        // CPU temp
                        if (user.Thresholds.Temps.Cpu != null)
                        {
                            machine.Thresholds.Temps.Cpu ??= new();
                            if (user.Thresholds.Temps.Cpu.Warn > 0)  machine.Thresholds.Temps.Cpu.Warn  = user.Thresholds.Temps.Cpu.Warn;
                            if (user.Thresholds.Temps.Cpu.Alert > 0) machine.Thresholds.Temps.Cpu.Alert = user.Thresholds.Temps.Cpu.Alert;
                        }
                        // GPU temp
                        if (user.Thresholds.Temps.Gpu != null)
                        {
                            machine.Thresholds.Temps.Gpu ??= new();
                            if (user.Thresholds.Temps.Gpu.Warn > 0)  machine.Thresholds.Temps.Gpu.Warn  = user.Thresholds.Temps.Gpu.Warn;
                            if (user.Thresholds.Temps.Gpu.Alert > 0) machine.Thresholds.Temps.Gpu.Alert = user.Thresholds.Temps.Gpu.Alert;
                        }
                        // Disk temp
                        if (user.Thresholds.Temps.Disk != null)
                        {
                            machine.Thresholds.Temps.Disk ??= new();
                            if (user.Thresholds.Temps.Disk.Warn > 0)  machine.Thresholds.Temps.Disk.Warn  = user.Thresholds.Temps.Disk.Warn;
                            if (user.Thresholds.Temps.Disk.Alert > 0) machine.Thresholds.Temps.Disk.Alert = user.Thresholds.Temps.Disk.Alert;
                        }
                    }
                }
            }
            catch { }
            return machine;
        }

        private int GetCpuWarn()  => _cfg?.Thresholds?.Cpu?.Warn   ?? 70;
        private int GetCpuAlert() => _cfg?.Thresholds?.Cpu?.Alert  ?? 90;
        private int GetRamWarn()  => _cfg?.Thresholds?.Ram?.Warn   ?? 70;
        private int GetRamAlert() => _cfg?.Thresholds?.Ram?.Alert  ?? 90;
        private int GetTempWarn(string part)
            => part switch {
                "Cpu"  => _cfg?.Thresholds?.Temps?.Cpu?.Warn  ?? 75,
                "Gpu"  => _cfg?.Thresholds?.Temps?.Gpu?.Warn  ?? 80,
                "Disk" => _cfg?.Thresholds?.Temps?.Disk?.Warn ?? 55,
                _ => 75
            };
        private int GetTempAlert(string part)
            => part switch {
                "Cpu"  => _cfg?.Thresholds?.Temps?.Cpu?.Alert  ?? 90,
                "Gpu"  => _cfg?.Thresholds?.Temps?.Gpu?.Alert  ?? 92,
                "Disk" => _cfg?.Thresholds?.Temps?.Disk?.Alert ?? 65,
                _ => 90
            };

        private void InitTimers()
        {
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            _survTimer.Interval = TimeSpan.FromSeconds(2);
            _survTimer.Tick += async (_, _) => await SurveillancePulse();

            _banterTimer.Tick += (_, _) =>
            {
                if (SurveillanceToggle.IsChecked == true)
                {
                    Say(PunchlineService.RandomBanter(), Mood.Playful);
                    _banterTimer.Interval = TimeSpan.FromMinutes(Random.Shared.Next(1, 7));
                }
            };
            _banterTimer.Interval = TimeSpan.FromMinutes(Random.Shared.Next(1, 7));
        }

        // =========================
        //   UI helpers
        // =========================
        private void ShowProgress()
        {
            ActionProgress.Visibility = Visibility.Visible;
            ActionProgress.IsIndeterminate = true;
        }

        private void HideProgress()
        {
            ActionProgress.IsIndeterminate = false;
            ActionProgress.Visibility = Visibility.Collapsed;
        }

        private void SetAvatarMood(string mood)
        {
            try { Avatar?.SetMood(mood); } catch { }
        }

        private void Say(string text, Mood mood)
        {
            var brush = mood switch
            {
                Mood.Happy   => new SolidColorBrush(Color.FromRgb(0x22,0x4E,0x2E)),
                Mood.Alert   => new SolidColorBrush(Color.FromRgb(0x4E,0x22,0x22)),
                Mood.Playful => new SolidColorBrush(Color.FromRgb(0x2E,0x2A,0x4E)),
                _            => new SolidColorBrush(Color.FromRgb(0x22,0x2A,0x32))
            };

            _chat.Add(new ChatItem
            {
                Text = text,
                BubbleBrush = brush,
                Time = DateTime.Now.ToString("HH:mm:ss")
            });

            SetAvatarMood(mood.ToString().ToLower());
            ChatScroll.ScrollToEnd();

            while (_chat.Count > 200) _chat.RemoveAt(0);
        }

        private static string Summarize(string big, int maxLines = 30)
        {
            if (string.IsNullOrWhiteSpace(big)) return string.Empty;
            var lines = big.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length <= maxLines) return big;
            return string.Join("\n", lines.Take(maxLines).Concat(new[] { $"… (+{lines.Length - maxLines} lignes)" }));
        }

        // =========================
        //   Surveillance
        // =========================
        private async Task SurveillancePulse()
        {
            try
            {
                var snap = await ReadSnapshotSafeAsync(); // usages + températures si dispo

                CpuBar.Value  = snap.Cpu.UsagePercent;
                GpuBar.Value  = snap.Gpu.UsagePercent;
                RamBar.Value  = snap.Ram.UsagePercent;
                DiskBar.Value = snap.Disk.UsagePercent;

                CpuTempText.Text  = snap.Cpu.TemperatureC.HasValue  ? $"CPU: {snap.Cpu.TemperatureC.Value:F0} °C"  : "CPU: -- °C";
                GpuTempText.Text  = snap.Gpu.TemperatureC.HasValue  ? $"GPU: {snap.Gpu.TemperatureC.Value:F0} °C"  : "GPU: -- °C";
                DiskTempText.Text = snap.Disk.TemperatureC.HasValue ? $"Disque: {snap.Disk.TemperatureC.Value:F0} °C" : "Disque: -- °C";
                RamText.Text      = $"RAM: {snap.Ram.UsedGiB:F1} / {snap.Ram.TotalGiB:F1} GiB";

                // Alerte si dépassement
                var cpuA = GetTempAlert("Cpu");
                var gpuA = GetTempAlert("Gpu");
                var dskA = GetTempAlert("Disk");
                if ((snap.Cpu.TemperatureC ?? 0) >= cpuA ||
                    (snap.Gpu.TemperatureC ?? 0) >= gpuA ||
                    (snap.Disk.TemperatureC ?? 0) >= dskA)
                {
                    Say("⚠️ Température élevée détectée.", Mood.Alert);
                    SetAvatarMood("devil");
                }
            }
            catch (Exception ex)
            {
                Say("Surveillance: " + ex.Message, Mood.Alert);
            }
        }

        private async Task<dynamic> ReadSnapshotSafeAsync()
        {
            // Essaie plusieurs noms/modes sur AdvancedMonitoringService (async/sync)
            var t = _monitor.GetType();
            var names = new[] { "GetSnapshotAsync", "ReadSnapshotAsync", "SnapshotAsync", "GetSnapshot", "ReadSnapshot" };

            foreach (var n in names)
            {
                var m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public);
                if (m == null) continue;

                var result = m.Invoke(_monitor, null);
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    var prop = task.GetType().GetProperty("Result");
                    if (prop != null) return prop.GetValue(task) ?? MakeEmptySnapshot();
                }
                return result ?? MakeEmptySnapshot();
            }

            return MakeEmptySnapshot();
        }

        private static dynamic MakeEmptySnapshot() => new
        {
            Cpu  = new { UsagePercent = 0, TemperatureC = (double?)null },
            Gpu  = new { UsagePercent = 0, TemperatureC = (double?)null },
            Ram  = new { UsagePercent = 0, UsedGiB = 0.0, TotalGiB = 0.0 },
            Disk = new { UsagePercent = 0, TemperatureC = (double?)null }
        };

        // =========================
        //   Top bar
        // =========================
        private void SurveillanceToggle_Checked(object sender, RoutedEventArgs e)
        {
            _survTimer.Start();
            _banterTimer.Start();
            StatusText.Text = "Surveillance ON";
            Say("Surveillance en direct activée.", Mood.Happy);
        }

        private void SurveillanceToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _survTimer.Stop();
            _banterTimer.Stop();
            StatusText.Text = "Surveillance OFF";
            Say("Surveillance arrêtée.", Mood.Neutral);
        }

        // =========================
        //   Actions
        // =========================
        private async void Action_MaintenanceComplete(object sender, RoutedEventArgs e)
        {
            ShowProgress();
            Say("Maintenance complète…", Mood.Neutral);
            try
            {
                var log = await _presets.FullAsync();
                Say(Summarize(log), Mood.Neutral);
                StatusText.Text = "Maintenance complète effectuée";
            }
            catch (Exception ex)
            {
                Say("❌ " + ex.Message, Mood.Alert);
                StatusText.Text = "Erreur maintenance";
            }
            finally { HideProgress(); }
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            ShowProgress();
            Say("Nettoyage des fichiers temporaires…", Mood.Neutral);
            try
            {
                var res = await _cleaning.CleanTempAsync();
                Say(Summarize(res), Mood.Neutral);
                StatusText.Text = "Nettoyage TEMP terminé";
            }
            catch (Exception ex)
            {
                Say("❌ " + ex.Message, Mood.Alert);
            }
            finally { HideProgress(); }
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            ShowProgress();
            Say("Nettoyage des navigateurs…", Mood.Neutral);
            try
            {
                var report = await _browsers.AnalyzeAndCleanAsync();
                Say(Summarize(report), Mood.Neutral);
                StatusText.Text = "Navigateurs nettoyés";
            }
            catch (Exception ex)
            {
                Say("❌ " + ex.Message, Mood.Alert);
            }
            finally { HideProgress(); }
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            ShowProgress();
            Say("Mise à jour globale du système…", Mood.Neutral);
            try
            {
                var a   = await _apps.UpgradeAllAsync();
                var d   = await _drivers.UpgradeDriversAsync();
                var s   = await _wu.StartScanAsync();
                var dl  = await _wu.StartDownloadAsync();
                var ins = await _wu.StartInstallAsync();
                var sig = await _def.UpdateSignaturesAsync();
                var scn = await _def.QuickScanAsync();

                var nl  = Environment.NewLine;
                var all = string.Join(nl, new[] { a, d, s, dl, ins, sig, scn }.Where(x => !string.IsNullOrWhiteSpace(x)));

                Say(Summarize(all), Mood.Neutral);
                StatusText.Text = "Mises à jour complètes effectuées";
                Say("✅ Tout est à jour !", Mood.Happy);
            }
            catch (Exception ex)
            {
                Say("❌ " + ex.Message, Mood.Alert);
                StatusText.Text = "Erreur mise à jour";
            }
            finally { HideProgress(); }
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            ShowProgress();
            Say("Sécurité Windows Defender…", Mood.Neutral);
            try
            {
                var sig = await _def.UpdateSignaturesAsync();
                var scn = await _def.QuickScanAsync();

                var nl  = Environment.NewLine;
                var msg = string.Join(nl, new[] { sig, scn }.Where(x => !string.IsNullOrWhiteSpace(x)));

                Say(Summarize(msg), Mood.Neutral);
                StatusText.Text = "Defender: signatures à jour + scan terminé";
            }
            catch (Exception ex)
            {
                Say("❌ Defender: " + ex.Message, Mood.Alert);
                StatusText.Text = "Erreur Defender";
            }
            finally { HideProgress(); }
        }

        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var where = GetConfigLocationsSafe();
                var machine = where.GetType().GetProperty("Machine")?.GetValue(where)?.ToString() ?? "-";
                var user    = where.GetType().GetProperty("User")?.GetValue(where)?.ToString() ?? "-";
                var nl = Environment.NewLine;
                Say("Config machine: " + machine + nl + "Config user: " + user, Mood.Neutral);
            }
            catch (Exception ex)
            {
                Say("Config: " + ex.Message, Mood.Alert);
            }
        }
    }

    public enum Mood { Neutral, Happy, Alert, Playful }

    public sealed class ChatItem
    {
        public string Text { get; set; } = "";
        public string Time { get; set; } = "";
        public Brush BubbleBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x22, 0x2A, 0x32));
    }
}
