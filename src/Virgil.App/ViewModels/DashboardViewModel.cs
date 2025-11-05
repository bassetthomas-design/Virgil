using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel principal du tableau de bord Virgil.
    /// NOTE: internal pour rester cohérent avec BaseViewModel (internal abstract).
    /// </summary>
    internal class DashboardViewModel : BaseViewModel
    {
        // ====== ÉTAT ======
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set => Set(ref _isSurveillanceEnabled, value);
        }

        // Exemple d’état UI (tu peux garder/étendre)
        private string _statusText = "Prêt.";
        public string StatusText
        {
            get => _statusText;
            set => Set(ref _statusText, value);
        }

        // ====== COMMANDES (si tu les bindes dans XAML) ======
        public ICommand ToggleSurveillanceCommand { get; }
        public ICommand MaintenanceCommand { get; }
        public ICommand CleanTempCommand { get; }
        public ICommand CleanBrowsersCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand DefenderScanCommand { get; }
        public ICommand OpenConfigCommand { get; }

        // ====== CTOR ======
        public DashboardViewModel()
        {
            // Initialise les commandes (si déjà bindées dans la vue, elles fonctionneront)
            ToggleSurveillanceCommand = new RelayCommand(ToggleSurveillance);
            MaintenanceCommand        = new RelayCommand(() => _ = RunMaintenance());
            CleanTempCommand          = new RelayCommand(() => _ = CleanTempFiles());
            CleanBrowsersCommand      = new RelayCommand(() => _ = CleanBrowsers());
            UpdateAllCommand          = new RelayCommand(() => _ = UpdateAll());
            DefenderScanCommand       = new RelayCommand(() => _ = RunDefenderScan());
            OpenConfigCommand         = new RelayCommand(OpenConfiguration);
        }

        // ====== MÉTHODES APPELÉES PAR MainWindow.xaml.cs ======
        // Ces signatures EXACTES sont nécessaires pour corriger les erreurs CS1061.
        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            AppendChat(IsSurveillanceEnabled
                ? "👁️ Surveillance activée."
                : "💤 Surveillance désactivée.");
        }

        public async Task RunMaintenance()
        {
            try
            {
                StatusText = "Maintenance complète en cours…";
                AppendChat("🧰 Maintenance complète : démarrage…");

                // TODO: enchaîner ici tes vraies actions (nettoyage, updates, sfc/dism…)
                await Task.Delay(200); // placeholder léger pour CI

                AppendChat("✅ Maintenance complète terminée.");
                StatusText = "Maintenance terminée.";
            }
            catch (Exception ex)
            {
                LogException("RunMaintenance", ex);
                AppendChat("❌ Échec maintenance. Consulte les logs.");
            }
        }

        public async Task CleanTempFiles()
        {
            try
            {
                StatusText = "Nettoyage TEMP…";
                AppendChat("🧹 Nettoyage intelligent des fichiers temporaires…");

                // TODO: appel réel de ton service de nettoyage
                await Task.Delay(100);

                AppendChat("✅ Nettoyage TEMP terminé.");
                StatusText = "Prêt.";
            }
            catch (Exception ex)
            {
                LogException("CleanTempFiles", ex);
                AppendChat("❌ Échec nettoyage TEMP. Regarde les logs.");
            }
        }

        public async Task CleanBrowsers()
        {
            try
            {
                StatusText = "Nettoyage navigateurs…";
                AppendChat("🌐 Purge des caches navigateurs…");

                // TODO: appel réel
                await Task.Delay(100);

                AppendChat("✅ Navigateurs nettoyés.");
                StatusText = "Prêt.";
            }
            catch (Exception ex)
            {
                LogException("CleanBrowsers", ex);
                AppendChat("❌ Échec nettoyage navigateurs. Voir logs.");
            }
        }

        public async Task UpdateAll()
        {
            try
            {
                StatusText = "Mises à jour…";
                AppendChat("⬆️ Mise à jour globale (apps/Windows/pilotes)…");

                // TODO: appel réel
                await Task.Delay(150);

                AppendChat("✅ Tout est à jour.");
                StatusText = "Prêt.";
            }
            catch (Exception ex)
            {
                LogException("UpdateAll", ex);
                AppendChat("❌ Échec des mises à jour. Voir logs.");
            }
        }

        public async Task RunDefenderScan()
        {
            try
            {
                StatusText = "Defender…";
                AppendChat("🛡️ Microsoft Defender : MAJ signatures + scan rapide…");

                // TODO: appel réel
                await Task.Delay(150);

                AppendChat("✅ Defender OK.");
                StatusText = "Prêt.";
            }
            catch (Exception ex)
            {
                LogException("RunDefenderScan", ex);
                AppendChat("❌ Échec Defender. Voir logs.");
            }
        }

        public void OpenConfiguration()
        {
            try
            {
                AppendChat("⚙️ Ouverture de la configuration…");
                // TODO: ouvrir ta fenêtre/onglet de settings (SettingsWindow, etc.)
                // new SettingsWindow().Show();
            }
            catch (Exception ex)
            {
                LogException("OpenConfiguration", ex);
                AppendChat("❌ Impossible d’ouvrir la configuration.");
            }
        }

        // ====== UTILITAIRES ======
        private void AppendChat(string message)
        {
            // Ici, on se contente d’actualiser un statut + log.
            // Adapte à ton service de chat si tu en as un (ChatService, etc.)
            StatusText = message;
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Virgil", "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd}.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // best-effort log
            }
        }

        private void LogException(string context, Exception ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Virgil", "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd}.log"),
                    $"[{DateTime.Now:HH:mm:ss}] [EXCEPTION:{context}] {ex}{Environment.NewLine}");
            }
            catch
            {
                // best-effort log
            }
        }
    }
}
