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
        private int _driverUpdatesFound;
        private string _driverUpdatesSummary = string.Empty;
        private bool _hasDriverScanResult;

        /// <summary>
        /// Commande appelée par les boutons d'ActionsPanel.xaml, avec l'identifiant d'action en paramètre.
        /// </summary>
        public ICommand InvokeActionCommand { get; }
        public ICommand ScanDriversCommand => _scanDriversCommand;
        public ICommand InstallDriversCommand => _installDriversCommand;

        public bool CanRunWindowsUpdate => !_isWindowsUpdateRunning;
        public bool IsBusy => _isDriverScanRunning || _isDriverInstallRunning;
        public bool CanScanDrivers => !IsBusy;
        public bool CanInstallDrivers => DriverUpdatesFound > 0 && !IsBusy;
        public bool HasDriverUpdates => DriverUpdatesFound > 0;
        public bool HasDriverScanResult => _hasDriverScanResult;
        public string DriverUpdatesSummary => _driverUpdatesSummary;
        public int DriverUpdatesFound
        {
            get => _driverUpdatesFound;
            private set
            {
                if (_driverUpdatesFound == value)
                {
                    return;
                }

                _driverUpdatesFound = value;
                OnPropertyChanged(nameof(DriverUpdatesFound));
            }
        }

        public ObservableCollection<DriverUpdateItem> DriverUpdateItems { get; } = new();

        public ActionsViewModel(Func<string, CancellationToken, Task<ActionResult>> runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _driverUpdateService = new DriverUpdateService();
            _scanDriversCommand = new AsyncRelayCommand(_ => ScanDriversAsync(), _ => CanScanDrivers);
            _installDriversCommand = new AsyncRelayCommand(_ => InstallDriversAsync(), _ => CanInstallDrivers);
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
                            await _runner(actionId!, CancellationToken.None).ConfigureAwait(false);
                        }
                        finally
                        {
                            _isWindowsUpdateRunning = false;
                            OnPropertyChanged(nameof(CanRunWindowsUpdate));
                        }

                        return;
                    }

                    await _runner(actionId!, CancellationToken.None).ConfigureAwait(false);
                }
            });
        }

        private async Task ScanDriversAsync()
        {
            if (IsBusy)
            {
                return;
            }

            _isDriverScanRunning = true;
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
            if (IsBusy || DriverUpdatesFound <= 0)
            {
                return;
            }

            _isDriverInstallRunning = true;
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

        private void UpdateDriverScanState(DriverUpdateResult result)
        {
            if (!result.Succeeded)
            {
                ResetDriverUpdates("Pilotes trouvés: 0");
                return;
            }

            DriverUpdateItems.Clear();
            foreach (var item in result.Items)
            {
                DriverUpdateItems.Add(item);
            }

            DriverUpdatesFound = DriverUpdateItems.Count;
            _driverUpdatesSummary = $"Pilotes trouvés: {DriverUpdatesFound}";
            _hasDriverScanResult = true;
            OnPropertyChanged(nameof(DriverUpdateItems));
            OnPropertyChanged(nameof(CanInstallDrivers));
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
            DriverUpdatesFound = Math.Max(DriverUpdatesFound - result.Installed, 0);
            _driverUpdatesSummary = $"Pilotes trouvés: {DriverUpdatesFound}";
            _hasDriverScanResult = true;
            OnPropertyChanged(nameof(DriverUpdateItems));
            OnPropertyChanged(nameof(CanInstallDrivers));
            NotifyDriverStateChanged();
        }

        private void ResetDriverUpdates(string summary)
        {
            DriverUpdateItems.Clear();
            DriverUpdatesFound = 0;
            _driverUpdatesSummary = summary;
            _hasDriverScanResult = true;
            OnPropertyChanged(nameof(DriverUpdateItems));
            OnPropertyChanged(nameof(CanInstallDrivers));
            NotifyDriverStateChanged();
        }

        private void NotifyDriverStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanScanDrivers));
            OnPropertyChanged(nameof(CanInstallDrivers));
            OnPropertyChanged(nameof(HasDriverUpdates));
            OnPropertyChanged(nameof(DriverUpdatesSummary));
            OnPropertyChanged(nameof(HasDriverScanResult));
            _scanDriversCommand.RaiseCanExecuteChanged();
            _installDriversCommand.RaiseCanExecuteChanged();
        }
    }
}
