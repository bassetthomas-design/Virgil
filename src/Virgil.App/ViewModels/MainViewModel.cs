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
using Virgil.Services.Chat;

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
            IConfirmationService confirmationService,
            ChatActionBridge? chatActionBridge = null,
            IChatEngine? chatEngine = null)
        {
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
            Monitoring = monitoring ?? throw new ArgumentNullException(nameof(monitoring));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _uiInteractions = uiInteractions ?? throw new ArgumentNullException(nameof(uiInteractions));
            _confirmationService = confirmationService ?? throw new ArgumentNullException(nameof(confirmationService));

            _isMonitoringEnabled = _settingsService.Settings.MonitoringEnabled;
            _isHudVisible = _settingsService.Settings.ShowMiniHud;

            _actionRegistry = BuildRegistry();

            Chat = new ChatViewModel(chat, chatActionBridge, chatEngine);

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
            var definitions = new List<ActionDefinition>();

            foreach (var descriptor in ActionCatalog.All.Values)
            {
                definitions.Add(MapAction(descriptor.ActionKey, descriptor.DisplayName, descriptor.IsDestructive, (_, ct) => RunBackendActionAsync(descriptor, ct)));
            }

            definitions.AddRange(new[]
            {
                MapAction("monitor_toggle", "Activer / désactiver la surveillance", false, (_, ct) => ToggleMonitoringAsync(ct)),
                MapAction("hud_toggle", "Afficher / masquer le HUD", false, (_, ct) => ToggleHudAsync(ct)),
                MapAction("open_settings", "Ouvrir les paramètres", false, (_, ct) => _uiInteractions.OpenSettingsAsync(ct)),
                MapAction("show_hud", "Afficher le HUD", false, (_, ct) => EnsureHudVisibleAsync(true, ct)),
                MapAction("hide_hud", "Masquer le HUD", false, (_, ct) => EnsureHudVisibleAsync(false, ct)),
                MapAction("actions_selftest", "Test actions", false, (_, ct) => ValidateRegistryAsync(ct)),
            });

            return new ActionRegistry(definitions);
        }

        private ActionDefinition MapAction(string key, string displayName, bool isDestructive, Func<Dictionary<string, string>?, CancellationToken, Task<ActionResult>> callback)
            => new(key, displayName, isDestructive, callback);

        private async Task<ActionResult> RunBackendActionAsync(ActionDescriptor descriptor, CancellationToken ct)
        {
            if (!descriptor.IsImplemented)
            {
                var unavailable = ActionResult.NotImplemented($"{descriptor.DisplayName}: non disponible ({descriptor.Service})");
                _chat.PostSystemMessage(unavailable.Message, MessageType.Warning, ChatKind.Warning);
                return unavailable;
            }

            var result = await _orchestrator.RunAsync(descriptor.VirgilActionId, ct).ConfigureAwait(false);
            var message = string.IsNullOrWhiteSpace(result.Message) ? descriptor.DisplayName : result.Message;
            if (result.TryGetDetails(out var details))
            {
                message = $"{message}\n{details}";
            }

            return new ActionResult(result.Success, message);
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

            var status = ActionCatalog.DescribeStatus();
            _chat.PostSystemMessage(status, MessageType.Info, ChatKind.Info);
            return await Task.FromResult(ActionResult.Completed($"{_actionRegistry.All.Count} actions câblées."));
        }
    }
}
