using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using Virgil.App.Chat;
using Virgil.App.Commands;
using Virgil.App.Interfaces;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Domain.Actions;
using Virgil.Core.Models;
using Virgil.Services.Abstractions;
using Virgil.Services.Assistant;
using Virgil.Services.Chat;
using Virgil.Services.SelfTest;
using ServiceActionExecutionResult = Virgil.Services.ActionExecutionResult;
using ServiceChatFormatter = Virgil.Services.ActionResultToChatFormatter;
using ServiceChatSeverity = Virgil.Services.ChatSeverity;

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
        private bool _lastActionSuccess;
        private string? _lastActionMessage;
        private ActionResult? _lastActionResult;
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
                Set(ref _isBusy, value);
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

        public ActionResult? LastActionResult
        {
            get => _lastActionResult;
            private set => Set(ref _lastActionResult, value);
        }

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
            IChatEngine? chatEngine = null,
            IAssistantService? assistantService = null)
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

            Chat = new ChatViewModel(
                chat,
                chatActionBridge,
                chatEngine,
                assistantService,
                BuildAssistantContext,
                RunActionAsync,
                settingsService);

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
            _monitoringService.SetIntervalRange(
                _settingsService.Settings.MonitoringIntervalMinutesMin,
                _settingsService.Settings.MonitoringIntervalMinutesMax);
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

        }

        public async Task ReloadUiFromSettingsAsync(CancellationToken ct)
        {
            _monitoringService.SetIntervalRange(
                _settingsService.Settings.MonitoringIntervalMinutesMin,
                _settingsService.Settings.MonitoringIntervalMinutesMax);
            _isMonitoringEnabled = _settingsService.Settings.MonitoringEnabled;
            if (_isMonitoringEnabled)
            {
                _monitoringService.Start();
            }
            else
            {
                _monitoringService.Stop();
            }

            OnPropertyChanged(nameof(MonitoringToggleLabel));

            Chat.DefaultTtlMs = _settingsService.Settings.DefaultMessageTtlMs;

            if (_settingsService.Settings.ShowMiniHud != _isHudVisible)
            {
                var hudResult = await EnsureHudVisibleAsync(_settingsService.Settings.ShowMiniHud, ct).ConfigureAwait(false);
                _isHudVisible = hudResult.Success && _settingsService.Settings.ShowMiniHud;
                OnPropertyChanged(nameof(HudToggleLabel));
            }
        }

        public void ResetTransientState()
        {
            LastActionMessage = null;
            LastActionSuccess = false;
            LastActionResult = null;
        }

        public async Task<ActionResult> RunActionAsync(string key, Dictionary<string, string>? args, CancellationToken ct)
        {
            Utils.StartupLog.Write($"UI action requested: {key}");
            var isServiceAction = ActionCatalog.All.ContainsKey(key);

            if (!_actionRegistry.TryGet(key, out var definition) || definition is null)
            {
                var missing = ActionResult.Failure($"Action inconnue: {key}");
                PostLocalActionResult(missing);
                LastActionSuccess = false;
                LastActionMessage = missing.Message;
                LastActionResult = missing;
                return missing;
            }

            if (definition.IsDestructive)
            {
                var confirmationMessage = definition.Key.Equals("network_hard_reset", StringComparison.OrdinalIgnoreCase)
                    ? "Confirmer l'action \"Reset réseau (complet)\" ? Avertissement : connexion perdue temporairement. Droits admin requis."
                    : $"Confirmer l'action \"{definition.DisplayName}\" ?";

                var confirmed = _confirmationService.Confirm(confirmationMessage, "Confirmation", System.Windows.MessageBoxImage.Warning);
                if (!confirmed)
                {
                    var cancelled = ActionResult.Skipped("Action annulée par l'utilisateur");
                    PostLocalActionResult(cancelled);
                    LastActionSuccess = cancelled.Status != ActionResultStatus.Failed;
                    LastActionMessage = cancelled.Message;
                    LastActionResult = cancelled;
                    return cancelled;
                }
            }

            try
            {
                IsBusy = true;

                var result = await definition.ExecuteAsync(args ?? new Dictionary<string, string>(), ct).ConfigureAwait(false);
                LastActionSuccess = result.Status != ActionResultStatus.Failed;
                LastActionMessage = result.Message;
                LastActionResult = result;
                if (!isServiceAction)
                {
                    PostLocalActionResult(result);
                }
                return result;
            }
            catch (Exception ex)
            {
                var failure = ActionResult.Failure($"Erreur pendant {definition.DisplayName}: {ex.Message}");
                LastActionSuccess = false;
                LastActionMessage = failure.Message;
                LastActionResult = failure;
                PostLocalActionResult(failure);
                Utils.StartupLog.Write($"Action {key} a échoué", ex);
                return failure;
            }
            finally
            {
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
                MapAction("monitor_refresh_now", "Rafraîchir maintenant", false, (_, ct) => RefreshMonitoringAsync(ct)),
                MapAction("hud_toggle", "Afficher / masquer le HUD", false, (_, ct) => ToggleHudAsync(ct)),
                MapAction("open_settings", "Ouvrir les paramètres", false, (_, ct) => _uiInteractions.OpenSettingsAsync(ct)),
                MapAction("show_hud", "Afficher le HUD", false, (_, ct) => EnsureHudVisibleAsync(true, ct)),
                MapAction("hide_hud", "Masquer le HUD", false, (_, ct) => EnsureHudVisibleAsync(false, ct)),
                MapAction("actions_selftest", "Test actions", false, (_, ct) => ValidateRegistryAsync(ct)),
                MapAction("copy_diagnostic", "Copier diagnostic", false, (args, ct) => CopyDiagnosticAsync(args, ct)),
            });

            return new ActionRegistry(definitions);
        }

        private AssistantContext BuildAssistantContext()
        {
            var telemetry = new AssistantTelemetrySummary(
                Monitoring.CpuUsageText,
                Monitoring.CpuUsageIsStale,
                Monitoring.RamUsageText,
                Monitoring.RamUsageIsStale,
                $"CPU {Monitoring.CpuTempText}, GPU {Monitoring.GpuTempText}",
                Monitoring.CpuTempIsStale || Monitoring.GpuTempIsStale,
                Monitoring.DiskUsageText,
                Monitoring.DiskUsageIsStale);

            var lastActionSummary = BuildAssistantLastActionSummary();
            var catalog = BuildAssistantCatalog();

            return new AssistantContext(telemetry, lastActionSummary, catalog);
        }

        private AssistantActionSummary? BuildAssistantLastActionSummary()
        {
            if (LastActionResult is null)
            {
                return null;
            }

            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(LastActionResult.Summary))
            {
                lines.AddRange(LastActionResult.Summary.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }

            var trimmed = lines.Take(3).Select(line => line.Trim()).ToList();
            return new AssistantActionSummary(LastActionResult.Status.ToString(), LastActionResult.Title, trimmed);
        }

        private IReadOnlyList<AssistantActionCatalogItem> BuildAssistantCatalog()
        {
            var catalog = new List<AssistantActionCatalogItem>();

            foreach (var definition in _actionRegistry.All)
            {
                var description = "Interface utilisateur";
                var requiresAdmin = definition.IsDestructive;
                var destructive = definition.IsDestructive;

                if (ActionCatalog.TryGet(definition.Key, out var descriptor))
                {
                    description = $"Service: {descriptor.Service}";
                    destructive = descriptor.IsDestructive;
                    requiresAdmin = descriptor.IsDestructive;
                }

                catalog.Add(new AssistantActionCatalogItem(
                    definition.Key,
                    definition.DisplayName,
                    description,
                    requiresAdmin,
                    destructive));
            }

            return catalog;
        }

        private ActionDefinition MapAction(string key, string displayName, bool isDestructive, Func<Dictionary<string, string>?, CancellationToken, Task<ActionResult>> callback)
            => new(key, displayName, isDestructive, callback);

        private async Task<ActionResult> RunBackendActionAsync(ActionDescriptor descriptor, CancellationToken ct)
        {
            if (!descriptor.IsImplemented)
            {
                var unavailableResult = ServiceActionExecutionResult.NotImplemented(
                    descriptor.DisplayName,
                    $"Action non implémentée ({descriptor.Service})");

                PostActionResultToChat(unavailableResult);
                return MapResult(unavailableResult);
            }

            var result = await _orchestrator.RunAsync(descriptor.VirgilActionId, ct).ConfigureAwait(false);
            return MapResult(result);
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

        private async Task<ActionResult> RefreshMonitoringAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await _monitoringService.RefreshNowAsync().ConfigureAwait(false);
            return ActionResult.Completed("Monitoring rafraîchi");
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

            var tester = new ActionWiringTester(_actionRegistry.All.Select(a => a.Key));
            var report = await tester.RunAsync(ActionCatalog.All.Values, ct).ConfigureAwait(false);

            var sb = new StringBuilder();
            foreach (var item in report.Items)
            {
                var statusLabel = item.Status switch
                {
                    ActionSelfTestStatus.Ok => "OK",
                    ActionSelfTestStatus.NonCablee => "Non câblée",
                    _ => "Erreur"
                };

                var reason = string.IsNullOrWhiteSpace(item.Reason) ? string.Empty : $" ({item.Reason})";
                sb.AppendLine($"Action {item.ActionNumber:00}: {statusLabel}{reason}");
            }

            sb.AppendLine($"{report.OkCount} / {report.Total} actions correctement câblées");

            _chat.PostSystemMessage(sb.ToString().TrimEnd(), MessageType.Info, ChatKind.Info);
            return ActionResult.Completed("Diagnostic câblage terminé");
        }

        private Task<ActionResult> CopyDiagnosticAsync(Dictionary<string, string>? args, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (args is null || !args.TryGetValue("text", out var text) || string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult(ActionResult.Failure("Aucun diagnostic à copier."));
            }

            try
            {
                Clipboard.SetText(text);
                return Task.FromResult(ActionResult.Completed("Diagnostic copié dans le presse-papiers."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ActionResult.Failure($"Copie du diagnostic impossible: {ex.Message}"));
            }
        }

        private static ActionResult MapResult(ServiceActionExecutionResult result)
            => new(result.Status, result.Title, result.Summary, result.Steps, result.Recommendations, result.DebugInfo);

        private void PostActionResultToChat(ServiceActionExecutionResult result)
        {
            var formatter = new ServiceChatFormatter();
            var formatted = formatter.Format(result);
            var (type, kind) = formatted.Severity switch
            {
                ServiceChatSeverity.Error => (MessageType.Error, ChatKind.Error),
                ServiceChatSeverity.Warning => (MessageType.Warning, ChatKind.Warning),
                _ => (MessageType.Info, ChatKind.Info)
            };

            _chat.PostSystemMessage(formatted.PrimaryMessage, type, kind);
            if (!string.IsNullOrWhiteSpace(formatted.Details))
            {
                _chat.PostSystemMessage(formatted.Details, MessageType.Info, ChatKind.Info);
            }
        }

        private void PostLocalActionResult(ActionResult result)
        {
            var (type, kind) = result.Status switch
            {
                ActionResultStatus.Failed => (MessageType.Error, ChatKind.Error),
                ActionResultStatus.PartialSuccess => (MessageType.Warning, ChatKind.Warning),
                ActionResultStatus.NotAvailable => (MessageType.Warning, ChatKind.Warning),
                ActionResultStatus.NotImplemented => (MessageType.Warning, ChatKind.Warning),
                ActionResultStatus.Skipped => (MessageType.Warning, ChatKind.Warning),
                _ => (MessageType.Info, ChatKind.Info)
            };

            var message = string.IsNullOrWhiteSpace(result.Message) ? result.Title : result.Message;
            _chat.PostSystemMessage(message, type, kind);
        }
    }
}
