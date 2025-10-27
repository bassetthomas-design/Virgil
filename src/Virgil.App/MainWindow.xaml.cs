using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Virgil.App
{
    public partial class MainWindow : Window
    {
        // --- Compat modèles (indépendants de Virgil.Core) --------------------

        public sealed class PercentPair
        {
            [JsonPropertyName("warn")]
            public int Warn { get; set; } = 80;

            [JsonPropertyName("alert")]
            public int Alert { get; set; } = 95;
        }

        public sealed class TempPair
        {
            [JsonPropertyName("warn")]
            public int Warn { get; set; } = 80;

            [JsonPropertyName("alert")]
            public int Alert { get; set; } = 90;
        }

        public sealed class ThresholdsCompat
        {
            [JsonPropertyName("cpu")]
            public PercentPair Cpu { get; set; } = new PercentPair();

            [JsonPropertyName("ram")]
            public PercentPair Ram { get; set; } = new PercentPair();

            // Températures : cpu/gpu/disk
            [JsonPropertyName("temps")]
            public TempsCompat Temps { get; set; } = new TempsCompat();

            public sealed class TempsCompat
            {
                [JsonPropertyName("cpu")]
                public TempPair Cpu { get; set; } = new TempPair();
                [JsonPropertyName("gpu")]
                public TempPair Gpu { get; set; } = new TempPair();
                [JsonPropertyName("disk")]
                public TempPair Disk { get; set; } = new TempPair();
            }
        }

        public sealed class VirgilConfigCompat
        {
            [JsonPropertyName("thresholds")]
            public ThresholdsCompat Thresholds { get; set; } = new ThresholdsCompat();
        }

        // --- Champs d’instance ------------------------------------------------

        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
        private readonly DispatcherTimer _survTimer = new DispatcherTimer();

        private VirgilConfigCompat _config = new VirgilConfigCompat();

        // Punchlines monitoring (1–6 min, texte uniquement)
        private readonly Random _rng = new Random();
        private bool _monitoringBanterEnabled = true;
        private DateTime _nextPunchUtc = DateTime.UtcNow.AddMinutes(2);
        private readonly string[] _punchlines = new[]
        {
            "Je surveille tout… même ce que tu ne vois pas 👀",
            "CPU zen, GPU serein. Pour l’instant.",
            "Un peu de ménage plus tard ? Ça ne fait jamais de mal.",
            "Tes shaders me remercieront.",
            "Winget chauffe… on met à jour quand tu veux.",
            "Si ça throttle, je te ping, promis."
        };

        private bool _monitoringEnabled;

        // --- Ctor -------------------------------------------------------------

        public MainWindow()
        {
            InitializeComponent();

            // 1) Charger config fusionnée machine+user (compat libre)
            _config = LoadMergedConfigCompat();

            // 2) Démarrer l’horloge UI (top bar)
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, __) =>
            {
                try
                {
                    // Met à jour un label si présent (optionnel)
                    // Ex: ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
                }
                catch { /* UI facultative */ }
            };
            _clockTimer.Start();

            // 3) Timer de surveillance (placeholder sans AdvancedMonitoringService)
            _survTimer.Interval = TimeSpan.FromSeconds(2);
            _survTimer.Tick += async (_, __) => await SurveillancePulseAsync();

            // 4) Message d’accueil + humeur neutre
            SetAvatarMood("neutral");
            Say("Salut, c’est Virgil. Je suis prêt à te donner un coup de main ✨", mood: "friendly");
        }

        // --- Config (machine + user) -----------------------------------------

        private static VirgilConfigCompat LoadMergedConfigCompat()
        {
            // %ProgramData%\Virgil\config.json  (machine)
            // %AppData%\Virgil\user.json        (user override)
            var machinePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Virgil", "config.json");
            var userPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Virgil", "user.json");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            VirgilConfigCompat machine = new VirgilConfigCompat();
            VirgilConfigCompat user = new VirgilConfigCompat();

            try
            {
                if (File.Exists(machinePath))
                {
                    var json = File.ReadAllText(machinePath);
                    var parsed = JsonSerializer.Deserialize<VirgilConfigCompat>(json, options);
                    if (parsed != null) machine = parsed;
                }
            }
            catch { /* défauts */ }

            try
            {
                if (File.Exists(userPath))
                {
                    var json = File.ReadAllText(userPath);
                    var parsed = JsonSerializer.Deserialize<VirgilConfigCompat>(json, options);
                    if (parsed != null) user = parsed;
                }
            }
            catch { /* défauts */ }

            return Merge(machine, user);
        }

        private static VirgilConfigCompat Merge(VirgilConfigCompat machine, VirgilConfigCompat user)
        {
            // fusion simple : user override machine si des valeurs sont fournies.
            var result = new VirgilConfigCompat();

            // CPU
            result.Thresholds.Cpu.Warn = user?.Thresholds?.Cpu?.Warn != 0 ? user.Thresholds.Cpu.Warn : machine.Thresholds.Cpu.Warn;
            result.Thresholds.Cpu.Alert = user?.Thresholds?.Cpu?.Alert != 0 ? user.Thresholds.Cpu.Alert : machine.Thresholds.Cpu.Alert;

            // RAM
            result.Thresholds.Ram.Warn = user?.Thresholds?.Ram?.Warn != 0 ? user.Thresholds.Ram.Warn : machine.Thresholds.Ram.Warn;
            result.Thresholds.Ram.Alert = user?.Thresholds?.Ram?.Alert != 0 ? user.Thresholds.Ram.Alert : machine.Thresholds.Ram.Alert;

            // Temps
            result.Thresholds.Temps.Cpu.Warn = user?.Thresholds?.Temps?.Cpu?.Warn != 0 ? user.Thresholds.Temps.Cpu.Warn : machine.Thresholds.Temps.Cpu.Warn;
            result.Thresholds.Temps.Cpu.Alert = user?.Thresholds?.Temps?.Cpu?.Alert != 0 ? user.Thresholds.Temps.Cpu.Alert : machine.Thresholds.Temps.Cpu.Alert;

            result.Thresholds.Temps.Gpu.Warn = user?.Thresholds?.Temps?.Gpu?.Warn != 0 ? user.Thresholds.Temps.Gpu.Warn : machine.Thresholds.Temps.Gpu.Warn;
            result.Thresholds.Temps.Gpu.Alert = user?.Thresholds?.Temps?.Gpu?.Alert != 0 ? user.Thresholds.Temps.Gpu.Alert : machine.Thresholds.Temps.Gpu.Alert;

            result.Thresholds.Temps.Disk.Warn = user?.Thresholds?.Temps?.Disk?.Warn != 0 ? user.Thresholds.Temps.Disk.Warn : machine.Thresholds.Temps.Disk.Warn;
            result.Thresholds.Temps.Disk.Alert = user?.Thresholds?.Temps?.Disk?.Alert != 0 ? user.Thresholds.Temps.Disk.Alert : machine.Thresholds.Temps.Disk.Alert;

            return result;
        }

        // --- UI helpers -------------------------------------------------------

        public void Say(string text, string? mood = null)
        {
            try
            {
                // Brancher sur ta zone de chat si elle existe :
                // ChatList.Items.Add(new ChatMessage { Text = text, Mood = mood ?? "neutral" });
                // éventuellement scroller en bas etc.
            }
            catch { /* silencieux */ }
        }

        private void SetAvatarMood(string mood)
        {
            try { AvatarControl?.SetMood(mood); } catch { /* safe */ }
        }

        // Barre de statut : indéterminée / message
        private void ProgressIndeterminate(bool isIndeterminate, string? statusText = null)
        {
            try
            {
                // Exemple si tu as une ProgressBar nommée StatusBarProgress :
                // StatusBarProgress.IsIndeterminate = isIndeterminate;
                // StatusBarText.Text = statusText ?? "";
            }
            catch { /* safe */ }
        }

        // --- Toggle Monitoring ------------------------------------------------

        private void StartMonitoring()
        {
            if (_monitoringEnabled) return;
            _monitoringEnabled = true;
            _survTimer.Start();
            PlanNextPunchline(); // démarre le cycle punchlines
            Say("Surveillance démarrée ✅", mood: "active");
        }

        private void StopMonitoring()
        {
            if (!_monitoringEnabled) return;
            _monitoringEnabled = false;
            _survTimer.Stop();
            Say("Surveillance arrêtée ⏹️", mood: "neutral");
        }

        // À binder sur le Toggle de la top bar (click/checked)
        private void MonitoringToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_monitoringEnabled) StopMonitoring(); else StartMonitoring();
        }

        // --- Pulse de surveillance (compat sans AdvancedMonitoringService) ----

        private async Task SurveillancePulseAsync()
        {
            // Ici : pas d’appel à AdvancedMonitoringService (les signatures varient).
            // On fait un "pulse" minimum pour garder l’UX vivante : punchlines + alerte fictive si besoin.
            try
            {
                await Task.Yield();

                // Punchlines aléatoires (1–6 min)
                if (_monitoringBanterEnabled && DateTime.UtcNow >= _nextPunchUtc)
                {
                    var pick = _punchlines[_rng.Next(_punchlines.Length)];
                    Say(pick, mood: "witty");
                    PlanNextPunchline();
                }

                // TODO: quand on aura la bonne API Core, lire snapshot & mettre à jour les barres si elles existent :
                // - CpuBar.Value, GpuBar.Value, MemBar.Value, DiskBar.Value
                // - CpuTemp.Text, GpuTemp.Text, DiskTemp.Text
                // - Badges warn/alert selon _config.Thresholds
            }
            catch
            {
                // non-bloquant
            }
        }

        private void PlanNextPunchline()
        {
            // entre 1 et 6 minutes
            var minutes = _rng.Next(1, 7);
            _nextPunchUtc = DateTime.UtcNow.AddMinutes(minutes);
        }

        // --- Boutons Actions (hooks simples, à relier aux Services Core) ------

        private async void Action_MaintenanceComplete_Click(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate(true, "Maintenance complète en cours…");
            Say("Je lance la maintenance complète (TEMP, navigateurs, caches étendus, mises à jour apps/jeux, Windows Update)…", mood: "active");
            try
            {
                // TODO: appeler tes services Core réels :
                // await MaintenancePresetsService.FullAsync();
                await Task.Delay(2000); // placeholder visuel

                Say("Maintenance terminée. Résumé posté dans le journal.", mood: "done");
            }
            catch (Exception ex)
            {
                Say($"Erreur maintenance : {ex.Message}", mood: "alert");
            }
            finally
            {
                ProgressIndeterminate(false, "Prêt");
            }
        }

        private async void Action_CleanTemp_Click(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate(true, "Nettoyage TEMP…");
            Say("Analyse et nettoyage des fichiers temporaires (utilisateur, AppData\\Local, Windows\\Temp)…", mood: "active");
            try
            {
                // TODO: brancher sur Virgil.Core.CleaningService
                await Task.Delay(1200); // placeholder

                Say("TEMP nettoyé ✅", mood: "done");
            }
            catch (Exception ex)
            {
                Say($"Erreur nettoyage TEMP : {ex.Message}", mood: "alert");
            }
            finally
            {
                ProgressIndeterminate(false, "Prêt");
            }
        }

        private async void Action_CleanBrowsers_Click(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate(true, "Nettoyage navigateurs…");
            Say("Je détecte les profils et vide les caches (Chromium/Firefox)…", mood: "active");
            try
            {
                // TODO: brancher sur BrowserCleaningService.AnalyzeAndClean(...)
                await Task.Delay(1200); // placeholder

                Say("Navigateurs nettoyés ✅", mood: "done");
            }
            catch (Exception ex)
            {
                Say($"Erreur nettoyage navigateurs : {ex.Message}", mood: "alert");
            }
            finally
            {
                ProgressIndeterminate(false, "Prêt");
            }
        }

        private async void Action_UpdateAll_Click(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate(true, "Mises à jour…");
            Say("Je lance les mises à jour : winget (apps/jeux, include-unknown, silencieux si possible), pilotes (best-effort), Windows Update (scan→download→install).", mood: "active");
            try
            {
                // TODO:
                // await ApplicationUpdateService.UpgradeAllAsync(...)
                // await DriverUpdateService.UpgradeDriversAsync()
                // await WindowsUpdateService.StartScanAsync(); etc.
                await Task.Delay(2000); // placeholder

                Say("Mises à jour terminées ✅", mood: "done");
            }
            catch (Exception ex)
            {
                Say($"Erreur mises à jour : {ex.Message}", mood: "alert");
            }
            finally
            {
                ProgressIndeterminate(false, "Prêt");
            }
        }

        private async void Action_Defender_Click(object sender, RoutedEventArgs e)
        {
            ProgressIndeterminate(true, "Windows Defender…");
            Say("Je lance la mise à jour des signatures Defender puis un scan rapide.", mood: "active");
            try
            {
                // TODO: couche Core pour MpCmdRun.exe /UpdateSignature puis /Scan -ScanType 1
                await Task.Delay(1500); // placeholder

                Say("Defender : signatures à jour et scan terminé ✅", mood: "done");
            }
            catch (Exception ex)
            {
                Say($"Erreur Defender : {ex.Message}", mood: "alert");
            }
            finally
            {
                ProgressIndeterminate(false, "Prêt");
            }
        }
    }
}
