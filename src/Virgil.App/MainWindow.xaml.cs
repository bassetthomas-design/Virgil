using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Virgil.App.Controls;
using Virgil.Core;
using Virgil.Core.Services;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // Timers
        private readonly DispatcherTimer _clockTimer = new();
        private readonly DispatcherTimer _survTimer  = new();   // monitoring pulse
        private readonly DispatcherTimer _banterTimer = new();  // punchlines 1-6 min

        // Services (Core)
        private readonly ConfigService _config = new();
        private readonly MaintenancePresetsService _presets = new();
        private readonly CleaningService _cleaning = new();
        private readonly BrowserCleaningService _browsers = new();
        private readonly ExtendedCleaningService _extended = new();
        private readonly ApplicationUpdateService _apps = new();
        private readonly DriverUpdateService _drivers = new();
        private readonly WindowsUpdateService _wu = new();
        private readonly DefenderUpdateService _def = new();
        private readonly AdvancedMonitoringService _monitor = new();

        // Chat
        private readonly ObservableCollection<ChatItem> _chat = new();

        // Seuils (fusion machine + user)
        private VirgilConfig _cfg;

        public MainWindow()
        {
            InitializeComponent();

            // Chat binding
            ChatList.ItemsSource = _chat;

            // Config (fusion machine + user)
            _cfg = _config.LoadMerged();
            ThresholdsText.Text =
                $"CPU warn/alert: {_cfg.Thresholds.Cpu.Warn}% / {_cfg.Thresholds.Cpu.Alert}%\n" +
                $"RAM warn/alert: {_cfg.Thresholds.Ram.Warn}% / {_cfg.Thresholds.Ram.Alert}%\n" +
                $"Temp CPU warn/alert: {_cfg.Thresholds.Temps.Cpu.Warn}°C / {_cfg.Thresholds.Temps.Cpu.Alert}°C\n" +
                $"Temp GPU warn/alert: {_cfg.Thresholds.Temps.Gpu.Warn}°C / {_cfg.Thresholds.Temps.Gpu.Alert}°C\n" +
                $"Temp Disk warn/alert: {_cfg.Thresholds.Temps.Disk.Warn}°C / {_cfg.Thresholds.Temps.Disk.Alert}°C";

            InitTimers();
            Say("Salut, je suis Virgil 👋", Mood.Neutral);
            SetAvatarMood("neutral");
        }

        private void InitTimers()
        {
            // Horloge live
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            _clockTimer.Start();

            // Pulse de surveillance
            _survTimer.Interval = TimeSpan.FromSeconds(2);
            _survTimer.Tick += async (_, _) => await SurveillancePulse();

            // Punchlines (1–6 min aléatoires) quand la surveillance est ON
            _banterTimer.Tick += (_, _) =>
            {
                if (SurveillanceToggle.IsChecked == true)
                {
                    Say(PunchlineService.RandomBanter(), Mood.Playful);
                    // prochaine occurrence 1-6 min
                    _banterTimer.Interval = TimeSpan.FromMinutes(Random.Shared.Next(1, 7));
                }
            };
            _banterTimer.Interval = TimeSpan.FromMinutes(Random.Shared.Next(1, 7));
        }

        // =========================
        //   UI helpers
        // =========================
        private void ProgressIndeterminate()
        {
            ActionProgress.Visibility = Visibility.Visible;
            ActionProgress.IsIndeterminate = true;
        }
        private void ProgressCollapsed()
        {
            ActionProgress.IsIndeterminate = false;
            ActionProgress.Visibility = Visibility.Collapsed;
        }

        private void SetAvatarMood(string mood)
        {
            try { Avatar?.SetMood(mood); } catch { /* safe */ }
        }

        private void Say(string text, Mood mood)
        {
            // Choix couleur de bulle selon humeur
            var brush = mood switch
            {
                Mood.Happy  => new SolidColorBrush(Color.FromRgb(0x22,0x4E,0x2E)),  // vert sombre
                Mood.Alert  => new SolidColorBrush(Color.FromRgb(0x4E,0x22,0x22)),  // rouge sombre
                Mood.Playful=> new SolidColorBrush(Color.FromRgb(0x2E,0x2A,0x4E)),  // violet sombre
                _           => new SolidColorBrush(Color.FromRgb(0x22,0x2A,0x32))
            };

            _chat.Add(new ChatItem
            {
                Text = text,
                BubbleBrush = brush,
                Time = DateTime.Now.ToString("HH:mm:ss")
            });

            // Pilotage avatar
            SetAvatarMood(mood.ToString().ToLower());
            // Auto-scroll en bas
            ChatScroll.ScrollToEnd();

            // Minifie l’historique si trop long
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
                var snap = await _monitor.ReadSnapshotAsync(); // usages + températures si dispo
                CpuBar.Value = snap.Cpu.UsagePercent;
                GpuBar.Value = snap.Gpu.UsagePercent;
                RamBar.Value = snap.Ram.UsagePercent;
                DiskBar.Value = snap.Disk.UsagePercent;

                CpuTempText.Text  = snap.Cpu.TemperatureC.HasValue  ? $"CPU: {snap.Cpu.TemperatureC.Value:F0} °C"  : "CPU: -- °C";
                GpuTempText.Text  = snap.Gpu.TemperatureC.HasValue  ? $"GPU: {snap.Gpu.TemperatureC.Value:F0} °C"  : "GPU: -- °C";
                DiskTempText.Text = snap.Disk.TemperatureC.HasValue ? $"Disque: {snap.Disk.TemperatureC.Value:F0} °C" : "Disque: -- °C";
                RamText.Text      = $"RAM: {snap.Ram.UsedGiB:F1} / {snap.Ram.TotalGiB:F1} GiB";

                // Alerte si dépassement
                if (snap.Cpu.TemperatureC >= _cfg.Thresholds.Temps.Cpu.Alert ||
                    snap.Gpu.TemperatureC >= _cfg.Thresholds.Temps.Gpu.Alert ||
                    snap.Disk.TemperatureC >= _cfg.Thresholds.Temps.Disk.Alert)
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

        // =========================
        //   Top bar events
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
            ProgressIndeterminate();
            Say("Maintenance complète…", Mood.Neutral);
            try
            {
                var log = await _presets.FullAsync(); // enchaîne cleaning/browsers/extended/winget/WU/drivers/defender
                Say(Summarize(log), Mood.Neutral);
                StatusText.Text = "Maintenance complète effectuée";
            }
            catch (Exception ex)
            {
                Say("❌ " + ex.Message, Mood.Alert);
                StatusText.Text = "Erreur maintenance";
            }
            finally { ProgressCollapsed(); }
        }

        private async void Action_CleanTemp(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate();
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
            finally { ProgressCollapsed(); }
        }

        private async void Action_CleanBrowsers(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate();
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
            finally { ProgressCollapsed(); }
        }

        private async void Action_UpdateAll(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate();
            Say("Mise à jour globale du système…", Mood.Neutral);
            try
            {
                // Apps/jeux (winget)
                var a = await _apps.UpgradeAllAsync(); // --all --include-unknown --silent (implémenté côté service)
                // Pilotes
                var d = await _drivers.UpgradeDriversAsync();
                // Windows Update
                var s = await _wu.StartScanAsync();
                var dl = await _wu.StartDownloadAsync();
                var ins = await _wu.StartInstallAsync();
                // Defender
                var sig = await _def.UpdateSignaturesAsync();
                var scan = await _def.QuickScanAsync();

                var all = string.Join("\n", new[] { a, d, s, dl, ins, sig, scan }.Where(x => !string.IsNullOrWhiteSpace(x)));
                Say(Summarize(all), Mood.Neutral);
                StatusText.Text = "Mises à jour complètes effectuées";
                Say("✅ Tout est à jour !", Mood.Happy);
            }
            catch (Exception ex)
            {
                Say("❌ " + ex.Message, Mood.Alert);
                StatusText.Text = "Erreur mise à jour";
            }
            finally { ProgressCollapsed(); }
        }

        private async void Action_Defender(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate();
            Say("Sécurité Windows Defender…", Mood.Neutral);
            try
            {
                var sig = await _def.UpdateSignaturesAsync(); // MAJ signatures
                var scan = await _def.QuickScanAsync();       // Quick scan (remplaçable par FullScanAsync)
                Say(Summarize(string.Join("\n", new[] { sig, scan })), Mood.Neutral);
                StatusText.Text = "Defender: signatures à jour + scan terminé";
            }
            catch (Exception ex)
            {
                Say("❌ Defender: " + ex.Message, Mood.Alert);
                StatusText.Text = "Erreur Defender";
            }
            finally { ProgressCollapsed(); }
        }

        // =========================
        //   Config
        // =========================
        private void OpenConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var where = _config.GetConfigLocations();
                Say("Config machine: " + where.Machine + "\nConfig user: " + where.User, Mood.Neutral);
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

    // Punchlines (périodiques 1–6 min quand surveillance ON)
    internal static class PunchlineService
    {
        private static readonly string[] Lines = new[]
        {
            "Routine ok. Tous les systèmes au vert.",
            "Je veille sur tes températures.",
            "Un petit nettoyage plus tard ?",
            "Winget est prêt à upgrader ce qui traîne.",
            "Si tu chauffes, je te préviens. Promis.",
            "Un scan Defender rapide ?"
        };

        public static string RandomBanter()
            => Lines[Random.Shared.Next(Lines.Length)];
    }
}
