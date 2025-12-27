using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Virgil.App.Chat;
using Virgil.App.Commands;
using Virgil.App.Interfaces;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Domain.Actions;
using Virgil.Services.Abstractions;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly ChatService _chat;
        private readonly MonitoringService _monitoringService;
        private readonly SettingsService _settingsService;
        private readonly IActionOrchestrator _orchestrator;
        private readonly IUiInteractionService _uiInteractions;
        private readonly IConfirmationService _confirmationService;
        private readonly ActionRegistry _actionRegistry;

        private bool _isBusy;
        private string _statusText = "Virgil est prêt";
        private string? _busyText;
        private bool _lastActionSuccess;
        private string? _lastActionMessage;
        private bool _isHudVisible;
        private bool _isMonitoringEnabled;

        public MonitoringViewModel Monitoring { get; }
        public ChatViewModel Chat { get; }
        public ActionsViewModel Actions { get; }

        public ICommand RunActionCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (Set(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (Set(ref _statusText, value))
                {
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        public string? BusyText
        {
            get => _busyText;
            private set
            {
                if (Set(ref _busyText, value))
                {
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        public bool LastActionSuccess
        {
            get => _lastActionSuccess;
            private set => Set(ref _lastActionSuccess, value);
        }

        public string? LastActionMessage
        {
            get => _lastActionMessage;
            private set => Set(ref _lastActionMessage, value);
        }

        public string StatusDisplay => IsBusy && !string.IsNullOrWhiteSpace(BusyText)
            ? BusyText!
            : StatusText;

        public string HudToggleLabel => _isHudVisible ? "Masquer HUD" : "Mini HUD";

        public string MonitoringToggleLabel => _isMonitoringEnabled ? "Désactiver la surveillance" : "Activer la surveillance";

        public MainViewModel(
            ChatService chat,
            MonitoringViewModel monitoring,
            IActionOrchestrator orchestrator,
            MonitoringService monitoringService,
            SettingsService settingsService,
            IUiInteractionService uiInteractions,
            IConfirmationService confirmationService)
        {
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
            Monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
            Chat = new ChatViewModel(chat);
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _uiInteractions = uiInteractions ?? throw new ArgumentNullException(nameof(uiInteractions));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));

            _isMonitoringEnabled = _settingsService.Settings.MonitoringEnabled;
            _isHudVisible = _settingsService.Settings.ShowMiniHud;

            _actionRegistry = BuildRegistry();

            RunActionCommand = new AsyncRelayCommand(async param =>
            {
                var key = param as string;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    await RunActionAsync(key!, null, CancellationToken.None).ConfigureAwait(false);
                }
            });

            Actions = new ActionsViewModel((key, ct) => RunActionAsync(key, null, ct));
        }

        public async Task InitializeAsync()
        {
            if (_settingsService.Settings.MonitoringEnabled)
            {
                _monitoringService.Start();
                _isMonitoringEnabled = true;
                OnPropertyChanged(nameof(MonitoringToggleLabel));
            }
            else
            {
                _monitoringService.Stop();
                _isMonitoringEnabled = false;
                OnPropertyChanged(nameof(MonitoringToggleLabel));
            }

            if (_settingsService.Settings.ShowMiniHud)
            {
                await ToggleHudAsync(CancellationToken.None).ConfigureAwait(false);
            }

            StatusText = "Virgil est prêt";
        }

        public async Task<ActionResult> RunActionAsync(string key, Dictionary<string, string>? args, CancellationToken ct)
        {
            Utils.StartupLog.Write($"UI action requested: {key}");

            if (!_actionRegistry.TryGet(key, out var definition) || definition is null)
            {
                var missing = ActionResult.Failure($"Action inconnue: {key}");
                StatusText = missing.Message;
                LastActionSuccess = false;
                LastActionMessage = missing.Message;
                return missing;
            }

            if (definition.IsDestructive)
            {
                var confirmed = _confirmationService.Confirm($"Confirmer l'action \"{definition.DisplayName}\" ?", "Confirmation", System.Windows.MessageBoxImage.Warning);
                if (!confirmed)
                {
                    var cancelled = ActionResult.Failure("Action annulée par l'utilisateur");
                    StatusText = cancelled.Message;
                    LastActionSuccess = false;
                    LastActionMessage = cancelled.Message;
                    return cancelled;
                }
            }

            try
            {
                IsBusy = true;
                BusyText = $"Exécution : {definition.DisplayName}";

                var result = await definition.ExecuteAsync(args ?? new Dictionary<string, string>(), ct).ConfigureAwait(false);
                LastActionSuccess = result.Success;
                LastActionMessage = result.Message;
                StatusText = string.IsNullOrWhiteSpace(result.Message)
                    ? definition.DisplayName
                    : result.Message;
                return result;
            }
            catch (Exception ex)
            {
                var failure = ActionResult.Failure($"Erreur pendant {definition.DisplayName}: {ex.Message}");
                LastActionSuccess = false;
                LastActionMessage = failure.Message;
                StatusText = failure.Message;
                Utils.StartupLog.Write($"Action {key} a échoué", ex);
                return failure;
            }
            finally
            {
                BusyText = null;
                IsBusy = false;
            }
        }

        private ActionRegistry BuildRegistry()
        {
            var definitions = new List<ActionDefinition>
            {
                MapAction("status", "Afficher le statut", false, (_, ct) => Task.FromResult(ActionResult.Completed("Statut demandé"))),
                MapAction("quick_scan", "Scan système express", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.ScanSystemExpress, "Scan système express", ct)),
                MapAction("quick_clean", "Nettoyage rapide", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.QuickClean, "Nettoyage rapide", ct)),
                MapAction("browser_soft_clean", "Nettoyage navigateur (léger)", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.LightBrowserClean, "Nettoyage navigateur (léger)", ct)),
                MapAction("browser_deep_clean", "Nettoyage navigateur (profond)", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.DeepBrowserClean, "Nettoyage navigateur (profond)", ct)),
                MapAction("ram_soft_free", "Libérer la RAM", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.SoftRamFlush, "Libération RAM", ct)),
                MapAction("deep_disk_clean", "Nettoyage disque avancé", true, (_, ct) => RunOrchestratorAsync(VirgilActionId.AdvancedDiskClean, "Nettoyage disque avancé", ct)),
                MapAction("network_diag", "Diagnostic réseau", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.NetworkQuickDiag, "Diagnostic réseau", ct)),
                MapAction("network_soft_reset", "Reset réseau (soft)", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.NetworkSoftReset, "Reset réseau (soft)", ct)),
                MapAction("network_hard_reset", "Reset réseau (complet)", true, (_, ct) => RunOrchestratorAsync(VirgilActionId.NetworkAdvancedReset, "Reset réseau (complet)", ct)),
                MapAction("network_latency_test", "Test de latence", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.LatencyStabilityTest, "Test de latence", ct)),
                MapAction("perf_mode_on", "Activer le mode performance", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.EnableGamingMode, "Mode performance", ct)),
                MapAction("perf_mode_off", "Désactiver le mode performance", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.RestoreNormalMode, "Mode normal", ct)),
                MapAction("startup_analyze", "Analyser le démarrage", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.StartupAnalysis, "Analyse démarrage", ct)),
                MapAction("gaming_kill_session", "Couper les apps de fond", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.CloseGamingSession, "Arrêt session gaming", ct)),
                MapAction("apps_update_all", "Mettre à jour les logiciels", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.UpdateSoftwares, "Mise à jour logiciels", ct)),
                MapAction("windows_update", "Mise à jour Windows", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.RunWindowsUpdate, "Mise à jour Windows", ct)),
                MapAction("gpu_driver_check", "Vérifier les drivers GPU", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.CheckGpuDrivers, "Vérification drivers GPU", ct)),
                MapAction("rambo_repair", "Mode RAMBO", true, (_, ct) => RunOrchestratorAsync(VirgilActionId.RamboMode, "Mode RAMBO", ct)),
                MapAction("chat_thanos", "Effet Thanos", true, (_, ct) => RunOrchestratorAsync(VirgilActionId.ThanosChatWipe, "Effet Thanos", ct)),
                MapAction("app_reload_settings", "Recharger la configuration", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.ReloadConfiguration, "Rechargement configuration", ct)),
                MapAction("monitoring_rescan", "Re-scanner le système", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.RescanSystem, "Re-scan système", ct)),
                MapAction("monitor_rescan", "Re-scanner le système", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.RescanSystem, "Re-scan système", ct)),
                MapAction("clean_browsers", "Nettoyage navigateurs", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.LightBrowserClean, "Nettoyage navigateurs", ct)),
                MapAction("maintenance_full", "Maintenance complète", false, (_, ct) => RunOrchestratorAsync(VirgilActionId.AdvancedDiskClean, "Maintenance complète", ct)),
                MapAction("monitor_toggle", "Activer / désactiver la surveillance", false, (_, ct) => ToggleMonitoringAsync(ct)),
                MapAction("hud_toggle", "Afficher / masquer le HUD", false, (_, ct) => ToggleHudAsync(ct)),
                MapAction("open_settings", "Ouvrir les paramètres", false, (_, ct) => _uiInteractions.OpenSettingsAsync(ct)),
                MapAction("show_hud", "Afficher le HUD", false, (_, ct) => EnsureHudVisibleAsync(true, ct)),
                MapAction("hide_hud", "Masquer le HUD", false, (_, ct) => EnsureHudVisibleAsync(false, ct)),
                MapAction("actions_selftest", "Test actions", false, (_, ct) => ValidateRegistryAsync(ct)),
            };

            return new ActionRegistry(definitions);
        }

        private ActionDefinition MapAction(string key, string displayName, bool isDestructive, Func<Dictionary<string, string>?, CancellationToken, Task<ActionResult>> callback)
            => new(key, displayName, isDestructive, callback);

        private async Task<ActionResult> RunOrchestratorAsync(VirgilActionId actionId, string displayName, CancellationToken ct)
        {
            await _orchestrator.RunAsync(actionId, ct).ConfigureAwait(false);
            return ActionResult.Completed($"{displayName} terminé.");
        }

        private Task<ActionResult> ToggleMonitoringAsync(CancellationToken ct)
        {
            _isMonitoringEnabled = !_isMonitoringEnabled;
            if (_isMonitoringEnabled)
            {
                _monitoringService.Start();
            }
            else
            {
                _monitoringService.Stop();
            }

            _settingsService.Settings.MonitoringEnabled = _isMonitoringEnabled;
            _settingsService.Save();
            OnPropertyChanged(nameof(MonitoringToggleLabel));
            return Task.FromResult(ActionResult.Completed(_isMonitoringEnabled ? "Surveillance activée" : "Surveillance désactivée"));
        }

        private async Task<ActionResult> ToggleHudAsync(CancellationToken ct)
        {
            if (_isHudVisible)
            {
                var hideResult = await _uiInteractions.HideHudAsync(ct).ConfigureAwait(false);
                _isHudVisible = false;
                _settingsService.Settings.ShowMiniHud = false;
                _settingsService.Save();
                OnPropertyChanged(nameof(HudToggleLabel));
                return hideResult;
            }

            var result = await _uiInteractions.ShowHudAsync(ct).ConfigureAwait(false);
            if (result.Success)
            {
                _isHudVisible = true;
                _settingsService.Settings.ShowMiniHud = true;
                _settingsService.Save();
                OnPropertyChanged(nameof(HudToggleLabel));
            }

            return result;
        }

        private async Task<ActionResult> EnsureHudVisibleAsync(bool shouldBeVisible, CancellationToken ct)
        {
            if (shouldBeVisible == _isHudVisible)
            {
                return ActionResult.Completed("État du HUD déjà conforme");
            }

            return await ToggleHudAsync(ct).ConfigureAwait(false);
        }

        private async Task<ActionResult> ValidateRegistryAsync(CancellationToken ct)
        {
            foreach (var definition in _actionRegistry.All)
            {
                if (definition.ExecuteAsync == null)
                {
                    return ActionResult.Failure($"Action sans implémentation: {definition.Key}");
                }
            }

            return await Task.FromResult(ActionResult.Completed($"{_actionRegistry.All.Count} actions câblées."));
        }
    }
}
