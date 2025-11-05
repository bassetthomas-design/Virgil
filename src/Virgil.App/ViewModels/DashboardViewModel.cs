using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Virgil.App.ViewModels
{
    // Fichier "autorité" : NE pas redéclarer ces mêmes membres dans d'autres fragments partiels.
    public partial class DashboardViewModel : INotifyPropertyChanged
    {
        // --- Etat de la surveillance (garder ICI uniquement) ---
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set
            {
                if (_isSurveillanceEnabled == value) return;
                _isSurveillanceEnabled = value;
                OnPropertyChanged(nameof(IsSurveillanceEnabled));
            }
        }

        // --- Commandes exposées à la MainWindow (garder ICI uniquement) ---
        public ICommand ToggleSurveillanceCommand { get; }
        public ICommand RunMaintenanceCommand { get; }
        public ICommand CleanTempFilesCommand { get; }
        public ICommand CleanBrowsersCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand RunDefenderScanCommand { get; }
        public ICommand OpenConfigurationCommand { get; }

        // --- Ctor (garder ICI uniquement) ---
        public DashboardViewModel()
        {
            ToggleSurveillanceCommand = new Relay(_ => ToggleSurveillance());
            RunMaintenanceCommand    = new Relay(async _ => await RunMaintenance());
            CleanTempFilesCommand    = new Relay(async _ => await CleanTempFiles());
            CleanBrowsersCommand     = new Relay(async _ => await CleanBrowsers());
            UpdateAllCommand         = new Relay(async _ => await UpdateAll());
            RunDefenderScanCommand   = new Relay(async _ => await RunDefenderScan());
            OpenConfigurationCommand = new Relay(_ => OpenConfiguration());
        }

        // --- Méthodes appelées par l'UI (garder ICI uniquement) ---
        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            Trace.WriteLine($"[VM] Surveillance → {(IsSurveillanceEnabled ? "ON" : "OFF")}");
        }

        public async Task RunMaintenance()
        {
            Trace.WriteLine("[VM] Maintenance complète demandée.");
            // TODO: Nettoyage intelligent → Navigateurs → Mises à jour (winget/Windows/Drivers/Defender)
            await Task.CompletedTask;
        }

        public async Task CleanTempFiles()
        {
            Trace.WriteLine("[VM] Nettoyage des fichiers temporaires.");
            // TODO: %TEMP%, Windows\Temp, Prefetch, logs, etc.
            await Task.CompletedTask;
        }

        public async Task CleanBrowsers()
        {
            Trace.WriteLine("[VM] Nettoyage des navigateurs.");
            // TODO: Chrome/Edge/Firefox caches (et options cookies)
            await Task.CompletedTask;
        }

        public async Task UpdateAll()
        {
            Trace.WriteLine("[VM] Mises à jour globales.");
            // TODO: winget upgrade --all ; Windows Update ; drivers ; Defender signatures
            await Task.CompletedTask;
        }

        public async Task RunDefenderScan()
        {
            Trace.WriteLine("[VM] Scan Microsoft Defender.");
            // TODO: MpCmdRun.exe -SignatureUpdate ; -Scan -ScanType 1
            await Task.CompletedTask;
        }

        public void OpenConfiguration()
        {
            Trace.WriteLine("[VM] Ouverture configuration.");
            // TODO: ouvrir fenêtre/onglet config, ou fichier JSON de config
        }

        // --- INotifyPropertyChanged (garder ICI uniquement) ---
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // --- Relay ICommand (garder ICI uniquement) ---
        public sealed class Relay : ICommand
        {
            private readonly Action<object?> _exec;
            private readonly Func<object?, bool>? _can;

            public Relay(Action<object?> exec, Func<object?, bool>? can = null)
            {
                _exec = exec ?? throw new ArgumentNullException(nameof(exec));
                _can  = can;
            }

            public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;

            public event EventHandler? CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public void Execute(object? parameter) => _exec(parameter);
        }
    }
}
