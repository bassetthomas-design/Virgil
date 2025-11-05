using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Virgil.App.ViewModels
{
    // IMPORTANT : partial pour cohabiter si tu as d’autres fragments (ex: DashboardViewModel.Commands.cs)
    public partial class DashboardViewModel
    {
        // --- Etat de la surveillance ---
        private bool _isSurveillanceEnabled;
        public bool IsSurveillanceEnabled
        {
            get => _isSurveillanceEnabled;
            set
            {
                if (_isSurveillanceEnabled == value) return;
                _isSurveillanceEnabled = value;
                OnPropertyChanged(nameof(IsSurveillanceEnabled));
                // Optionnel : lever un événement pour l’UI/Avatar
            }
        }

        // --- Commandes exposées à la MainWindow (liées aux boutons/handlers) ---
        public ICommand ToggleSurveillanceCommand { get; }
        public ICommand RunMaintenanceCommand { get; }
        public ICommand CleanTempFilesCommand { get; }
        public ICommand CleanBrowsersCommand { get; }
        public ICommand UpdateAllCommand { get; }
        public ICommand RunDefenderScanCommand { get; }
        public ICommand OpenConfigurationCommand { get; }

        // --- Ctor ---
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

        // --- Handlers appelés par MainWindow.xaml.cs ---
        public void ToggleSurveillance()
        {
            IsSurveillanceEnabled = !IsSurveillanceEnabled;
            // TODO: démarrer/arrêter la boucle de monitoring, punchlines, etc.
            Trace.WriteLine($"[VM] Surveillance toggled → {(IsSurveillanceEnabled ? "ON" : "OFF")}");
        }

        public async Task RunMaintenance()
        {
            Trace.WriteLine("[VM] Maintenance complète demandée.");
            // TODO: enchaîner Nettoyage intelligent → Navigateurs → Mises à jour (winget/Windows/Drivers/Defender)
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
            // TODO: Chrome/Edge/Firefox caches (et options cookies si activées)
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
            // TODO: ouvrir la fenêtre/onglet de configuration, ou ouvrir le fichier JSON de config
        }

        // --- Infrastructure INotifyPropertyChanged minimale ---
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        // --- Relay simple pour les ICommand ---
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

            // L’avertissement CS0067 que tu voyais auparavant venait d’un event non utilisé :
            public event EventHandler? CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public void Execute(object? parameter) => _exec(parameter);
        }
    }
}
