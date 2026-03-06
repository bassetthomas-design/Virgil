using System;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Commands;
using Virgil.App.Models;
using Virgil.Core.Models;
using Virgil.Core.Services;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel pour le panneau d'actions rapides.
    /// Reçoit un identifiant d'action (Tag / CommandParameter) et délègue au backend.
    /// </summary>
    public class ActionsViewModel : BaseViewModel
    {
        private readonly Func<string, CancellationToken, Task<ActionResult>> _runner;
        private readonly DriverUpdateService _driverUpdateService;
        private readonly AsyncRelayCommand _scanDriversCommand;
        private readonly AsyncRelayCommand _installDriversCommand;
        private bool _isWindowsUpdateRunning;
        private bool _isDriverScanRunning;
        private bool _isDriverInstallRunning;
        private bool _isActionRunning;
        private bool _isSecurityScanRunning;
        private bool _isDefenderFullScanRunning;
        private CancellationTokenSource? _defenderFullScanCts;
        private int _driversFoundCount;
        private string _driverUpdatesSummary = string.Empty;
        private bool _hasDriverScanResult;
        private bool _hasStartupAnalysis;
        private bool _hasStartupRecommendations;
        private bool _hasStartupRestoreEntries;
        private string _startupOptimizeTooltip = string.Empty;

        /// <summary>
        /// Commande appelée par les boutons d'ActionsPanel.xaml, avec l'identifiant d'action en paramètre.
        /// </summary>
        public ICommand InvokeActionCommand { get; }
        public ICommand ScanDriversCommand => _scanDriversCommand;
        public ICommand InstallDriversCommand => _installDriversCommand;
        public ICommand RunDefenderQuickScanCommand { get; }
        public ICommand RunDefenderFullScanCommand { get; }
        public ICommand RunWindowsMalwareScanCommand { get; }
        public ICommand CancelDefenderFullScanCommand { get; }

        public bool CanRunWindowsUpdate => !_isWindowsUpdateRunning;
        public bool IsBusy => _isDriverScanRunning || _isDriverInstallRunning || _isActionRunning || _isSecurityScanRunning;
        public bool IsSecurityScanRunning => _isSecurityScanRunning;
        public bool IsDefenderFullScanRunning => _isDefenderFullScanRunning;
        public bool CanCancelDefenderFullScan => _isDefenderFullScanRunning && _defenderFullScanCts is not null;
        public bool CanScanDrivers => !IsBusy;
        public bool CanInstallDrivers => DriversFoundCount > 0 && !IsBusy;
        public bool HasDriverUpdates => DriversFoundCount > 0;
        public bool HasDriverScanResult => _hasDriverScanResult;
        public bool HasStartupAnalysis => _hasStartupAnalysis;
        public bool HasStartupRecommendations => _hasStartupRecommendations;
        public bool HasStartupRestoreEntries => _hasStartupRestoreEntries;
        public bool CanOptimizeStartup => HasStartupAnalysis && !IsBusy;
        public bool CanRestoreStartup => HasStartupRestoreEntries && !IsBusy;
        public string StartupOptimizeTooltip => _startupOptimizeTooltip;
        public string DriverUpdatesSummary => _driverUpdatesSummary;
        public bool IsDriverOperationInProgress => _isDriverScanRunning || _isDriverInstallRunning;
        public int DriversFoundCount
        {
            get => _driversFoundCount;
            private set
            {
                if (_driversFoundCount == value)
                {
                    return;
                }

                _driversFoundCount = value;
                OnPropertyChanged(nameof(DriversFoundCount));
            }
        }

        public int DriverUpdatesFound => DriversFoundCount;
        public ObservableCollection<DriverUpdateItem> DriverUpdateItems { get; } = new();

        public ActionsViewModel(Func<string, CancellationToken, Task<ActionResult>> runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _driverUpdateService = new DriverUpdateService();
            _scanDriversCommand = new AsyncRelayCommand(_ => ScanDriversAsync(), _ => CanScanDrivers);
            _installDriversCommand = new AsyncRelayCommand(_ => InstallDriversAsync(), _ => CanInstallDrivers);
            RunDefenderQuickScanCommand = new AsyncRelayCommand(_ => RunSecurityActionAsync("defender_quick_scan"), _ => !IsBusy);
            RunDefenderFullScanCommand = new AsyncRelayCommand(_ => RunDefenderFullScanAsync(), _ => !IsBusy);
            RunWindowsMalwareScanCommand = new AsyncRelayCommand(_ => RunSecurityActionAsync("windows_malware_scan"), _ => !IsBusy);
            CancelDefenderFullScanCommand = new RelayCommand(_ => CancelDefenderFullScan(), _ => CanCancelDefenderFullScan);
            RefreshStartupRestoreState();
            InvokeActionCommand = new AsyncRelayCommand(async param =>
            {
                var actionId = param as string;
                if (!string.IsNullOrWhiteSpace(actionId))
                {
                    if (string.Equals(actionId, "drivers_scan", StringComparison.OrdinalIgnoreCase))
                    {
                        await ScanDriversAsync().ConfigureAwait(false);
                        return;
                    }

                    if (string.Equals(actionId, "drivers_install", StringComparison.OrdinalIgnoreCase))
                    {
                        await InstallDriversAsync().ConfigureAwait(false);
                        return;
                    }

                    if (string.Equals(actionId, "windows_update", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_isWindowsUpdateRunning)
                        {
                            return;
                        }

                        _isWindowsUpdateRunning = true;
                        OnPropertyChanged(nameof(CanRunWindowsUpdate));
                        try
                        {
                            await RunActionAsync(actionId!).ConfigureAwait(false);
                        }
                        finally
                        {
                            _isWindowsUpdateRunning = false;
                            OnPropertyChanged(nameof(CanRunWindowsUpdate));
                        }

                        return;
                    }

                    await RunActionAsync(actionId!).ConfigureAwait(false);
                }
            });
        }

        private async Task RunActionAsync(string actionId)
        {
            if (_isActionRunning)
            {
                return;
            }

            _isActionRunning = true;
            NotifyDriverStateChanged();
            try
            {
                var result = await _runner(actionId, CancellationToken.None).ConfigureAwait(false);
                if (string.Equals(actionId, "startup_analyze", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateStartupAnalysisState(result);
                }

                if (string.Equals(actionId, "startup_optimize", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionId, "startup_restore", StringComparison.OrdinalIgnoreCase))
                {
                    RefreshStartupRestoreState();
                    if (result.Success)
                    {
                        var refreshed = await _runner("startup_analyze", CancellationToken.None).ConfigureAwait(false);
                        UpdateStartupAnalysisState(refreshed);
                    }
                }
            }
            finally
            {
                _isActionRunning = false;
                NotifyDriverStateChanged();
            }
        }

        private async Task ScanDriversAsync()
        {
            if (IsBusy)
            {
                return;
            }

            _isDriverScanRunning = true;
            _driverUpdatesSummary = "Je vérifie les pilotes disponibles.";
            NotifyDriverStateChanged();
            try
            {
                var result = await _driverUpdateService.ScanAsync(CancellationToken.None).ConfigureAwait(false);
                UpdateDriverScanState(result);
            }
            finally
            {
                _isDriverScanRunning = false;
                NotifyDriverStateChanged();
            }
        }

        private async Task InstallDriversAsync()
        {
            if (IsBusy || DriversFoundCount <= 0)
            {
                return;
            }

            _isDriverInstallRunning = true;
            _driverUpdatesSummary = $"{DriversFoundCount} pilote(s) trouvé(s). Installation en cours.";
            NotifyDriverStateChanged();
            try
            {
                var items = new List<DriverUpdateItem>(DriverUpdateItems);
                var result = await _driverUpdateService.InstallAsync(items, CancellationToken.None).ConfigureAwait(false);
                UpdateDriverInstallState(result);
            }
            catch (Exception)
            {
                _driverUpdatesSummary = "Installation des pilotes échouée.";
                _hasDriverScanResult = true;
                NotifyDriverStateChanged();
            }
            finally
            {
                _isDriverInstallRunning = false;
                NotifyDriverStateChanged();
            }
        }


        private async Task RunSecurityActionAsync(string actionId)
        {
            if (IsBusy)
            {
                return;
            }

            _isSecurityScanRunning = true;
            NotifyDriverStateChanged();
            try
            {
                await RunActionAsync(actionId).ConfigureAwait(false);
            }
            finally
            {
                _isSecurityScanRunning = false;
                NotifyDriverStateChanged();
            }
        }

        private async Task RunDefenderFullScanAsync()
        {
            if (IsBusy)
            {
                return;
            }

            _defenderFullScanCts = new CancellationTokenSource();
            _isSecurityScanRunning = true;
            _isDefenderFullScanRunning = true;
            NotifyDriverStateChanged();

            try
            {
                await _runner("defender_full_scan", _defenderFullScanCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _defenderFullScanCts.Dispose();
                _defenderFullScanCts = null;
                _isDefenderFullScanRunning = false;
                _isSecurityScanRunning = false;
                NotifyDriverStateChanged();
            }
        }

        private void CancelDefenderFullScan()
        {
            _defenderFullScanCts?.Cancel();
        }

        private void UpdateDriverScanState(DriverUpdateResult result)
        {
            if (!result.Succeeded)
            {
                ResetDriverUpdates("Aucun pilote disponible.");
                return;
            }

            DriverUpdateItems.Clear();
            foreach (var item in result.Items)
            {
                DriverUpdateItems.Add(item);
            }

            DriversFoundCount = DriverUpdateItems.Count;
            _driverUpdatesSummary = DriversFoundCount == 0
                ? "Aucun pilote disponible."
                : $"{DriversFoundCount} pilote(s) trouvé(s). Installation en cours.";
            _hasDriverScanResult = true;
            OnPropertyChanged(nameof(DriverUpdateItems));
            OnPropertyChanged(nameof(CanInstallDrivers));
            OnPropertyChanged(nameof(DriversFoundCount));
            NotifyDriverStateChanged();
        }

        private void UpdateDriverInstallState(DriverUpdateResult result)
        {
            if (!result.Succeeded)
            {
                _driverUpdatesSummary = "Installation des pilotes échouée.";
                _hasDriverScanResult = true;
                NotifyDriverStateChanged();
                return;
            }

            DriverUpdateItems.Clear();
            DriversFoundCount = Math.Max(DriversFoundCount - result.Installed, 0);
            _driverUpdatesSummary = result.RebootRequired
                ? $"Pilotes: {result.Installed} installé(s). Redémarrage requis."
                : $"Pilotes: {result.Installed} installé(s).";
            _hasDriverScanResult = true;
            OnPropertyChanged(nameof(DriverUpdateItems));
            OnPropertyChanged(nameof(CanInstallDrivers));
            OnPropertyChanged(nameof(DriversFoundCount));
            NotifyDriverStateChanged();
        }

        private void ResetDriverUpdates(string summary)
        {
            DriverUpdateItems.Clear();
            DriversFoundCount = 0;
            _driverUpdatesSummary = summary;
            _hasDriverScanResult = true;
            OnPropertyChanged(nameof(DriverUpdateItems));
            OnPropertyChanged(nameof(CanInstallDrivers));
            OnPropertyChanged(nameof(DriversFoundCount));
            NotifyDriverStateChanged();
        }

        private void UpdateStartupAnalysisState(ActionResult result)
        {
            if (!result.Success)
            {
                _hasStartupAnalysis = false;
                _hasStartupRecommendations = false;
                _startupOptimizeTooltip = string.Empty;
                NotifyStartupStateChanged();
                return;
            }

            _hasStartupAnalysis = true;
            _hasStartupRecommendations = result.Recommendations?.Count > 0;
            _startupOptimizeTooltip = _hasStartupRecommendations ? string.Empty : "Rien à optimiser";
            NotifyStartupStateChanged();
        }

        private void RefreshStartupRestoreState()
        {
            _hasStartupRestoreEntries = false;
            try
            {
                _hasStartupRestoreEntries = Virgil.Services.Startup.StartupOptimizationService.HasStartupDisabledEntries();
            }
            catch
            {
                _hasStartupRestoreEntries = false;
            }

            NotifyStartupStateChanged();
        }

        private void NotifyStartupStateChanged()
        {
            OnPropertyChanged(nameof(HasStartupAnalysis));
            OnPropertyChanged(nameof(HasStartupRecommendations));
            OnPropertyChanged(nameof(HasStartupRestoreEntries));
            OnPropertyChanged(nameof(CanOptimizeStartup));
            OnPropertyChanged(nameof(CanRestoreStartup));
            OnPropertyChanged(nameof(StartupOptimizeTooltip));
        }

        private void NotifyDriverStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanScanDrivers));
            OnPropertyChanged(nameof(CanInstallDrivers));
            OnPropertyChanged(nameof(HasDriverUpdates));
            OnPropertyChanged(nameof(IsDriverOperationInProgress));
            OnPropertyChanged(nameof(DriverUpdatesSummary));
            OnPropertyChanged(nameof(HasDriverScanResult));
            OnPropertyChanged(nameof(CanOptimizeStartup));
            OnPropertyChanged(nameof(CanRestoreStartup));
            OnPropertyChanged(nameof(IsSecurityScanRunning));
            OnPropertyChanged(nameof(IsDefenderFullScanRunning));
            OnPropertyChanged(nameof(CanCancelDefenderFullScan));
            _scanDriversCommand.RaiseCanExecuteChanged();
            _installDriversCommand.RaiseCanExecuteChanged();
            if (RunDefenderQuickScanCommand is AsyncRelayCommand quickScanCommand)
            {
                quickScanCommand.RaiseCanExecuteChanged();
            }

            if (RunDefenderFullScanCommand is AsyncRelayCommand fullScanCommand)
            {
                fullScanCommand.RaiseCanExecuteChanged();
            }

            if (RunWindowsMalwareScanCommand is AsyncRelayCommand mrtScanCommand)
            {
                mrtScanCommand.RaiseCanExecuteChanged();
            }

            if (CancelDefenderFullScanCommand is RelayCommand cancelFullScanCommand)
            {
                cancelFullScanCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
