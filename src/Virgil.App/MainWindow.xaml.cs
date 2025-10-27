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
        private readonly DispatcherTimer _clockTimer  = new DispatcherTimer();
        private readonly DispatcherTimer _survTimer   = new DispatcherTimer();   // monitoring pulse
        private readonly DispatcherTimer _banterTimer = new DispatcherTimer();   // punchlines 1–6 min

        // Services (Core)
        private readonly CfgService _config = new CfgService();
        private readonly MaintenancePresetsService _presets = new MaintenancePresetsService();
        private readonly Virgil.Core.Services.CleaningService _cleaning = new Virgil.Core.Services.CleaningService();
        private readonly BrowserCleaningService _browsers = new BrowserCleaningService();
        private readonly ExtendedCleaningService _extended = new ExtendedCleaningService();
        private readonly ApplicationUpdateService _apps = new ApplicationUpdateService();
        private readonly DriverUpdateService _drivers = new DriverUpdateService();
        private readonly WindowsUpdateService _wu = new WindowsUpdateService();
        private readonly DefenderUpdateService _def = new DefenderUpdateService();
        private readonly AdvancedMonitoringService _monitor = new AdvancedMonitoringService();

        // Chat
        private readonly ObservableCollection<ChatItem> _chat = new ObservableCollection<ChatItem>();

        // Config fusionnée (machine + user)
        private CfgModel _cfg = new CfgModel();

        public MainWindow()
        {
            InitializeComponent();

            ChatList.ItemsSource = _chat;

            // Charger config en mode "safe"
            _cfg = LoadConfigSafe();

            var nl = Environment.NewLine;
            ThresholdsText.Text =
                "CPU warn/alert: " + GetCpuWarn() + "% / " + GetCpuAlert() + "%" + nl +
                "RAM warn/alert: " + GetRamWarn() + "% / " + GetRamAlert() + "%" + nl +
                "Temp CPU warn/alert: " + GetTempWarn("Cpu") + "°C / " + GetTempAlert("Cpu") + "°C" + nl +
                "Temp GPU warn/alert: " + GetTempWarn("Gpu") + "°C / " + GetTempAlert("Gpu") + "°C" + nl +
                "Temp Disk warn/alert: " + GetTempWarn("Disk") + "°C / " + GetTempAlert("Disk") + "°C";

            InitTimers();
            Say("Salut, je suis Virgil !", Mood.Neutral);
            SetAvatarMood("neutral");
        }

        // =========================
        //   SAFE helpers (config)
        // =========================
        private CfgModel LoadConfigSafe()
        {
            try
            {
                // Tente .LoadMerged()
                MethodInfo m = typeof(CfgService).GetMethod("LoadMerged", BindingFlags.Instance | BindingFlags.Public);
                if (m != null)
                {
                    object v = m.Invoke(_config, null);
                    if (v is CfgModel ok1) return ok1;
                }
                // Tente .Load()
                m = typeof(CfgService).GetMethod("Load", BindingFlags.Instance | BindingFlags.Public);
                if (m != null)
                {
                    object v = m.Invoke(_config, null);
                    if (v is CfgModel ok2) return ok2;
                }
            }
            catch
            {
                // ignore
            }

            // Fallback: lit %ProgramData% puis override %AppData%
            try
            {
                string machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "config.json");
                string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "user.json");
                CfgModel cfg = new CfgModel();

                if (File.Exists(machine))
                {
                    CfgModel c = JsonSerializer.Deserialize<CfgModel>(File.ReadAllText(machine));
                    if (c != null) cfg = c;
                }
                if (File.Exists(user))
                {
                    CfgModel u = JsonSerializer.Deserialize<CfgModel>(File.ReadAllText(user));
                    if (u != null) cfg = MergeUserOnMachine(cfg, u);
                }
                return cfg;
            }
            catch
            {
                // ignore
            }

            return new CfgModel();
        }

        private object GetConfigLocationsSafe()
        {
            try
            {
                MethodInfo m = typeof(CfgService).GetMethod("GetConfigLocations", BindingFlags.Instance | BindingFlags.Public);
                if (m != null)
                {
                    object v = m.Invoke(_config, null);
                    if (v != null) return v;
                }
            }
            catch
            {
                // ignore
            }

            string machine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Virgil", "config.json");
            string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Virgil", "user.json");
            return new { Machine = machine, User = user };
        }

        private static CfgModel MergeUserOnMachine(CfgModel machine, CfgModel user)
        {
            try
            {
                if (user != null && user.Thresholds != null)
                {
                    if (machine.Thresholds == null) machine.Thresholds = new CfgModel.ThresholdsModel();

                    // CPU
                    if (user.Thresholds.Cpu != null)
                    {
                        if (machine.Thresholds.Cpu == null) machine.Thresholds.Cpu = new CfgModel.PercentThreshold();
                        if (user.Thresholds.Cpu.Warn > 0) machine.Thresholds.Cpu.Warn = user.Thresholds.Cpu.Warn;
                        if (user.Thresholds.Cpu.Alert > 0) machine.Thresholds.Cpu.Alert = user.Thresholds.Cpu.Alert;
                    }
                    // RAM
                    if (user.Thresholds.Ram != null)
                    {
                        if (machine.Thresholds.Ram == null) machine.Thresholds.Ram = new CfgModel.PercentThreshold();
                        if (user.Thresholds.Ram.Warn > 0) machine.Thresholds.Ram.Warn = user.Thresholds.Ram.Warn;
                        if (user.Thresholds.Ram.Alert > 0) machine.Thresholds.Ram.Alert = user.Thresholds.Ram.Alert;
                    }
                    // Temps
                    if (user.Thresholds.Temps != null)
                    {
                        if (machine.Thresholds.Temps == null) machine.Thresholds.Temps = new CfgModel.TempThresholds();

                        if (user.Thresholds.Temps.Cpu != null)
                        {
                            if (machine.Thresholds.Temps.Cpu == null) machine.Thresholds.Temps.Cpu = new CfgModel.TempThreshold();
                            if (user.Thresholds.Temps.Cpu.Warn > 0) machine.Thresholds.Temps.Cpu.Warn = user.Thresholds.Temps.Cpu.Warn;
                            if (user.Thresholds.Temps.Cpu.Alert > 0) machine.Thresholds.Temps.Cpu.Alert = user.Thresholds.Temps.Cpu.Alert;
                        }
                        if (user.Thresholds.Temps.Gpu != null)
                        {
                            if (machine.Thresholds.Temps.Gpu == null) machine.Thresholds.Temps.Gpu = new CfgModel.TempThreshold();
                            if (user.Thresholds.Temps.Gpu.Warn > 0) machine.Thresholds.Temps.Gpu.Warn = user.Thresholds.Temps.Gpu.Warn;
                            if (user.Thresholds.Temps.Gpu.Alert > 0) machine.Thresholds.Temps.Gpu.Alert = user.Thresholds.Temps.Gpu.Alert;
                        }
                        if (user.Thresholds.Temps.Disk != null)
                        {
                            if (machine.Thresholds.Temps.Disk == null) machine.Thresholds.Temps.Disk = new CfgModel.TempThreshold();
                            if (user.Thresholds.Temps.Disk.Warn > 0) machine.Thresholds.Temps.Disk.Warn = user.Thresholds.Temps.Disk.Warn;
                            if (user.Thresholds.Temps.Disk.Alert > 0) machine.Thresholds.Temps.Disk.Alert = user.Thresholds.Temps.Disk.Alert;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
            return machine;
        }

        private int GetCpuWarn() { return (_cfg != null && _cfg.Thresholds != null && _cfg.Thresholds.Cpu != null) ? _cfg.Thresholds.Cpu.Warn : 70; }
        private int GetCpuAlert() { return (_cfg != null && _cfg.Thresholds != null && _cfg.Thresholds.Cpu != null) ? _cfg.Thresholds.Cpu.Alert : 90; }
        private int GetRamWarn() { return (_cfg != null && _cfg.Thresholds != null && _cfg.Thresholds.Ram != null) ? _cfg.Thresholds.Ram.Warn : 70; }
        private int GetRamAlert() { return (_cfg != null && _cfg.Thresholds != null && _cfg.Thresholds.Ram != null) ? _cfg.Thresholds.Ram.Alert : 90; }
        private int GetTempWarn(string part)
        {
            if (_cfg == null || _cfg.Thresholds == null || _cfg.Thresholds.Temps == null) return 75;
            if (part == "Cpu" && _cfg.Thresholds.Temps.Cpu != null) return _cfg.Thresholds.Temps.Cpu.Warn;
            if (part == "Gpu" && _cfg.Thresholds.Temps.Gpu != null) return _cfg.Thresholds.Temps.Gpu.Warn;
            if (part == "Disk" && _cfg.Thresholds.Temps.Disk != null) return _cfg.Thresholds.Temps.Disk.Warn;
            return 75;
        }
        private int GetTempAlert(string part)
        {
            if (_cfg == null || _cfg.Thresholds == null || _cfg.Thresholds.Temps == null) return 90;
            if (part == "Cpu" && _cfg.Thresholds.Temps.Cpu != null) return _cfg.Thresholds.Temps.Cpu.Alert;
            if (part == "Gpu" && _cfg.Thresholds.Temps.Gpu != null) return _cfg.Thresholds.Temps.Gpu.Alert;
            if (part == "Disk" && _cfg.Thresholds.Temps.Disk != null) return _cfg.Thresholds.Temps.Disk.Alert;
            return 90;
        }

        private void InitTimers()
        {
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) => { ClockText.Text = DateTime.Now.ToString("HH:mm:ss"); };
            _clockTimer.Start();

            _survTimer.Interval = TimeSpan.FromSeconds(2);
            _survTimer.Tick += async (s, e) => { await SurveillancePulse(); };

            _banterTimer.Tick += (s, e) =>
            {
                if (SurveillanceToggle.IsChecked == true)
                {
                    Say(PunchlineService.RandomBanter(), Mood.Playful);
                    _banterTimer.Interval = TimeSpan.FromMinutes(new Random().Next(1, 7));
                }
            };
            _banterTimer.Interval = TimeSpan.FromMinutes(new Random().Next(1, 7));
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
            try { if (Avatar != null) Avatar.SetMood(mood); } catch { }
        }

        private void Say(string text, Mood mood)
        {
            SolidColorBrush brush;
            if (mood == Mood.Happy) brush = new SolidColorBrush(Color.FromRgb(0x22, 0x4E, 0x2E));
            else if (mood == Mood.Alert) brush = new SolidColorBrush(Color.FromRgb(0x4E, 0x22, 0x22));
            else if (mood == Mood.Playful) brush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2A, 0x4E));
            else brush = new SolidColorBrush(Color.FromRgb(0x22, 0x2A, 0x32));

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

        private static string Summarize(string big, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(big)) return string.Empty;
            string[] lines = big.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length <= maxLines) return big;
            return string.Join("\n", lines.Take(maxLines).Concat(new string[] { "… (+" + (lines.Length - maxLines) + " lignes)" }));
        }

        // =========================
        //   Surveillance
        // =========================
        private async Task SurveillancePulse()
        {
            try
            {
                dynamic snap = await ReadSnapshotSafeAsync();

                CpuBar.Value  = Convert.ToDouble(GetDyn(snap, "Cpu", "UsagePercent", 0.0));
                GpuBar.Value  = Convert.ToDouble(GetDyn(snap, "Gpu", "UsagePercent", 0.0));
                RamBar.Value  = Convert.ToDouble(GetDyn(snap, "Ram", "UsagePercent", 0.0));
                DiskBar.Value = Convert.ToDouble(GetDyn(snap, "Disk", "UsagePercent", 0.0));

                double? cpuT  = GetDynNullableDouble(snap, "Cpu", "TemperatureC");
                double? gpuT  = GetDynNullableDouble(snap, "Gpu", "TemperatureC");
                double? dskT  = GetDynNullableDouble(snap, "Disk", "TemperatureC");
                double usedGiB = Convert.ToDouble(GetDyn(snap, "Ram", "UsedGiB", 0.0));
                double totGiB  = Convert.ToDouble(GetDyn(snap, "Ram", "TotalGiB", 0.0));

                CpuTempText.Text  = (cpuT.HasValue ? "CPU: " + cpuT.Value.ToString("F0") + " °C" : "CPU: -- °C");
                GpuTempText.Text  = (gpuT.HasValue ? "GPU: " + gpuT.Value.ToString("F0") + " °C" : "GPU: -- °C");
                DiskTempText.Text = (dskT.HasValue ? "Disque: " + dskT.Value.ToString("F0") + " °C" : "Disque: -- °C");
                RamText.Text      = "RAM: " + usedGiB.ToString("F1") + " / " + totGiB.ToString("F1") + " GiB";

                int cpuA = GetTempAlert("Cpu");
                int gpuA = GetTempAlert("Gpu");
                int dskA = GetTempAlert("Disk");
                if ((cpuT ?? 0) >= cpuA || (gpuT ?? 0) >= gpuA || (dskT ?? 0) >= dskA)
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
            Type t = _monitor.GetType();
            string[] names = new string[] { "GetSnapshotAsync", "ReadSnapshotAsync", "SnapshotAsync", "GetSnapshot", "ReadSnapshot" };

            for (int i = 0; i < names.Length; i++)
            {
                MethodInfo m = t.GetMethod(names[i], BindingFlags.Instance | BindingFlags.Public);
                if (m == null) continue;

                object result = m.Invoke(_monitor, null);
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    PropertyInfo pr = task.GetType().GetProperty("Result");
                    if (pr != null)
                    {
                        object val = pr.GetValue(task);
                        return val ?? MakeEmptySnapshot();
                    }
                    return MakeEmptySnapshot();
                }
                return result ?? MakeEmptySnapshot();
            }

            return MakeEmptySnapshot();
        }

        private static object MakeEmptySnapshot()
        {
            return new
            {
                Cpu = new { UsagePercent = 0.0, TemperatureC = (double?)null },
                Gpu = new { UsagePercent = 0.0, TemperatureC = (double?)null },
                Ram = new { UsagePercent = 0.0, UsedGiB = 0.0, TotalGiB = 0.0 },
                Disk = new { UsagePercent = 0.0, TemperatureC = (double?)null }
            };
        }

        private static object GetDyn(object root, string part, string prop, object fallback)
        {
            if (root == null) return fallback;
            PropertyInfo pPart = root.GetType().GetProperty(part);
            if (pPart == null) return fallback;
            object vPart = pPart.GetValue(root);
            if (vPart == null) return fallback;
            PropertyInfo pProp = vPart.GetType().GetProperty(prop);
            if (pProp == null) return fallback;
            object v = pProp.GetValue(vPart);
            return v ?? fallback;
        }

        private static double? GetDynNullableDouble(object root, string part, string prop)
        {
            object v = GetDyn(root, part, prop, null);
            if (v == null) return null;
            try { return Convert.ToDouble(v); } catch { return null; }
        }

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
                string log = await _presets.FullAsync();
                Say(Summarize(log, 30), Mood.Neutral);
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
                string res = await _cleaning.CleanTempAsync();
                Say(Summarize(res, 30), Mood.Neutral);
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
                string report = await _browsers.AnalyzeAndCleanAsync();
                Say(Summarize(report, 30), Mood.Neutral);
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
                string a   = await _apps.UpgradeAllAsync();
                string d   = await _drivers.UpgradeDriversAsync();
                string s   = await _wu.StartScanAsync();
                string dl  = await _wu.StartDownloadAsync();
                string ins = await _wu.StartInstallAsync();
                string sig = await _def.UpdateSignaturesAsync();
                string scn = await _def.QuickScanAsync();

                string nl = Environment.NewLine;
                string all = string.Join(nl, new string[] { a, d, s, dl, ins, sig, scn }.Where(x => !string.IsNullOrWhiteSpace(x)));

                Say(Summarize(all, 30), Mood.Neutral);
                StatusText.Text = "Mises à jour complètes effectuées";
                Say("Tout est à jour !", Mood.Happy);
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
                string sig = await _def.UpdateSignaturesAsync();
                string scn = await _def.QuickScanAsync();

                string nl = Environment.NewLine;
                string msg = string.Join(nl, new string[] { sig, scn }.Where(x => !string.IsNullOrWhiteSpace(x)));

                Say(Summarize(msg, 30), Mood.Neutral);
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
                object where = GetConfigLocationsSafe();
                PropertyInfo pM = where.GetType().GetProperty("Machine");
                PropertyInfo pU = where.GetType().GetProperty("User");
                string machine = (pM != null ? Convert.ToString(pM.GetValue(where)) : "-") ?? "-";
                string user = (pU != null ? Convert.ToString(pU.GetValue(where)) : "-") ?? "-";
                string nl = Environment.NewLine;
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
