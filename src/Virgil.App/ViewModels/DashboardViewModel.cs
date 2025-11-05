// File: src/Virgil.App/ViewModels/DashboardViewModel.cs
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// Vue-modèle tableau de bord.
    /// NOTE: Version unifiée pour supprimer tous les doublons (_isSurveillanceEnabled,
    /// IsSurveillanceEnabled, ToggleSurveillance, RunMaintenance, etc.).
    /// Si tu avais d'autres "partial" pour DashboardViewModel, laisse-les,
    /// mais assure-toi qu'ils ne redéfinissent PAS ces mêmes membres.
    /// </summary>
    public class DashboardViewModel : INotifyPropertyChanged
    {
        // === State ===
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set
            {
                if (value == _isSurveillanceEnabled) return;
                _isSurveillanceEnabled = value;
                OnPropertyChanged();
            }
        }

        // === INotifyPropertyChanged ===
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // === Helpers de log minimalistes pour la CI (pas de dépendances externes) ===
        private static void CiLog(string message)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var logDir = Path.Combine(baseDir, "logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "ci-vm.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Au pire, on trace dans Debug
                Debug.WriteLine(message);
            }
        }

        // === Méthodes appelées par MainWindow ===
        // Important: gardées avec ces signatures pour éviter les CS1061
        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            CiLog($"ToggleSurveillance => {IsSurveillanceEnabled}");
        }

        public async Task RunMaintenance()
        {
            CiLog("RunMaintenance invoked.");
            // TODO: brancher Virgil.Agent / pipeline réel
            await Task.CompletedTask;
        }

        public async Task CleanTempFiles()
        {
            CiLog("CleanTempFiles invoked.");
            // TODO: implémentation réelle de nettoyage
            await Task.CompletedTask;
        }

        public async Task CleanBrowsers()
        {
            CiLog("CleanBrowsers invoked.");
            // TODO: nettoyage navigateurs
            await Task.CompletedTask;
        }

        public async Task UpdateAll()
        {
            CiLog("UpdateAll invoked.");
            // TODO: winget upgrade + Windows Update + drivers
            await Task.CompletedTask;
        }

        public async Task RunDefenderScan()
        {
            CiLog("RunDefenderScan invoked.");
            // TODO: Update signatures + scan rapide
            await Task.CompletedTask;
        }

        public void OpenConfiguration()
        {
            CiLog("OpenConfiguration invoked.");
            // TODO: ouverture de la fenêtre de config / fichier JSON
        }
    }
}
