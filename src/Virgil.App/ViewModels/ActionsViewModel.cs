using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Commands;
using Virgil.App.Models;

namespace Virgil.App.ViewModels
{
    /// <summary>
    /// ViewModel pour le panneau d'actions rapides.
    /// Reçoit un identifiant d'action (Tag / CommandParameter) et délègue au backend.
    /// </summary>
    public class ActionsViewModel : BaseViewModel
    {
        private readonly Func<string, CancellationToken, Task<ActionResult>> _runner;
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

        public bool CanRunWindowsUpdate => !_isWindowsUpdateRunning;
        public bool CanScanDrivers => !_isDriverScanRunning && !_isDriverInstallRunning;
        public bool CanInstallDrivers => HasDriverUpdates && !_isDriverScanRunning && !_isDriverInstallRunning;
        public bool HasDriverUpdates => _driverUpdatesFound > 0;
        public bool HasDriverScanResult => _hasDriverScanResult;
        public string DriverUpdatesSummary => _driverUpdatesSummary;

        public ActionsViewModel(Func<string, CancellationToken, Task<ActionResult>> runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            InvokeActionCommand = new AsyncRelayCommand(async param =>
            {
                var actionId = param as string;
                if (!string.IsNullOrWhiteSpace(actionId))
                {
                    if (string.Equals(actionId, "drivers_scan", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_isDriverScanRunning || _isDriverInstallRunning)
                        {
                            return;
                        }

                        _isDriverScanRunning = true;
                        NotifyDriverStateChanged();
                        try
                        {
                            var result = await _runner(actionId!, CancellationToken.None).ConfigureAwait(false);
                            UpdateDriverStateFromResult(result, isInstall: false);
                        }
                        finally
                        {
                            _isDriverScanRunning = false;
                            NotifyDriverStateChanged();
                        }

                        return;
                    }

                    if (string.Equals(actionId, "drivers_install", StringComparison.OrdinalIgnoreCase))
                    {
                        if (_isDriverScanRunning || _isDriverInstallRunning || !HasDriverUpdates)
                        {
                            return;
                        }

                        _isDriverInstallRunning = true;
                        NotifyDriverStateChanged();
                        try
                        {
                            var result = await _runner(actionId!, CancellationToken.None).ConfigureAwait(false);
                            UpdateDriverStateFromResult(result, isInstall: true);
                        }
                        finally
                        {
                            _isDriverInstallRunning = false;
                            NotifyDriverStateChanged();
                        }

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

        private void UpdateDriverStateFromResult(ActionResult result, bool isInstall)
        {
            if (result.Status == ActionResultStatus.Failed || result.Status == ActionResultStatus.NotAvailable)
            {
                _driverUpdatesFound = 0;
                _driverUpdatesSummary = "0 mise(s) à jour de pilotes trouvée(s)";
                _hasDriverScanResult = true;
                NotifyDriverStateChanged();
                return;
            }

            var parsed = TryParseDriverDebugInfo(result.DebugInfo);
            if (parsed.HasValue)
            {
                var found = parsed.Value.Found;
                var installed = parsed.Value.Installed;
                _driverUpdatesFound = isInstall ? Math.Max(found - installed, 0) : found;
                _driverUpdatesSummary = $"{_driverUpdatesFound} mise(s) à jour de pilotes trouvée(s)";
                _hasDriverScanResult = true;
                NotifyDriverStateChanged();
            }
        }

        private void NotifyDriverStateChanged()
        {
            OnPropertyChanged(nameof(CanScanDrivers));
            OnPropertyChanged(nameof(CanInstallDrivers));
            OnPropertyChanged(nameof(HasDriverUpdates));
            OnPropertyChanged(nameof(DriverUpdatesSummary));
            OnPropertyChanged(nameof(HasDriverScanResult));
        }

        private static (int Found, int Installed)? TryParseDriverDebugInfo(string? debugInfo)
        {
            if (string.IsNullOrWhiteSpace(debugInfo))
            {
                return null;
            }

            int? found = null;
            int? installed = null;
            var parts = debugInfo.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (kvp.Length != 2)
                {
                    continue;
                }

                if (kvp[0].Equals("drivers_found", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(kvp[1], out var parsedFound))
                {
                    found = parsedFound;
                }
                else if (kvp[0].Equals("drivers_installed", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(kvp[1], out var parsedInstalled))
                {
                    installed = parsedInstalled;
                }
            }

            if (!found.HasValue || !installed.HasValue)
            {
                return null;
            }

            return (found.Value, installed.Value);
        }
    }
}
