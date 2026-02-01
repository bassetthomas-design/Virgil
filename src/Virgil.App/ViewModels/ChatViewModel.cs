using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Virgil.App.Chat;
using Virgil.App.Commands;
using Virgil.App.Models;
using Virgil.App.Services;
using Virgil.Core.Logging;
using Virgil.Services.Assistant;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MessageItem> Messages { get; } = new();
        private readonly ChatService _chat;
        private readonly Virgil.Services.Chat.ChatActionBridge? _actionBridge;
        private readonly Virgil.Services.Chat.IChatEngine? _chatEngine;
        private readonly IAssistantService? _assistantService;
        private readonly SettingsService? _settingsService;
        private readonly Func<AssistantContext>? _assistantContextProvider;
        private readonly Func<string, Dictionary<string, string>?, CancellationToken, Task<ActionResult>>? _actionExecutor;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private string _inputText = string.Empty;
        private bool _isBusy;
        private string _aiStatusText = "État IA : Chargement…";
        private Brush _aiStatusBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA43A"));
        private bool _isAiLoading = true;
        private string? _aiStatusTooltip;
        private bool _isChatReady;
        private LocalLlamaStateSnapshot _latestLlamaSnapshot;
        private readonly DispatcherTimer _aiStatusTimer;
        private static readonly Brush ReadyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3CCB7F"));
        private static readonly Brush StartingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA43A"));
        private static readonly Brush FailedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5A5A"));
        private const int MinChatTtlMs = 180000;
        private const int ExpireRetrySeconds = 5;
        private int _defaultTtlMs = MinChatTtlMs;

        public ChatViewModel(
            ChatService chat,
            Virgil.Services.Chat.ChatActionBridge? bridge = null,
            Virgil.Services.Chat.IChatEngine? engine = null,
            IAssistantService? assistantService = null,
            Func<AssistantContext>? assistantContextProvider = null,
            Func<string, Dictionary<string, string>?, CancellationToken, Task<ActionResult>>? actionExecutor = null,
            SettingsService? settingsService = null)
        {
            _chat = chat;
            _actionBridge = bridge;
            _chatEngine = engine;
            _assistantService = assistantService;
            _assistantContextProvider = assistantContextProvider;
            _actionExecutor = actionExecutor;
            _settingsService = settingsService;
            _chat.MessagePosted += OnMessagePosted;
            _chat.HistoryCleared += OnHistoryCleared;
            SendCommand = new RelayCommand(_ => _ = SendAsync(), _ => CanSend());
            ExecuteProposedActionCommand = new AsyncRelayCommand(ExecuteProposedActionAsync);
            CopyMessageCommand = new RelayCommand(param => CopyMessage(param as MessageItem));

            AiPillBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22FFFFFF"));
            var localState = LocalLlamaStateService.Instance;
            localState.StateUpdated += OnLocalLlamaStateUpdated;
            _latestLlamaSnapshot = localState.Snapshot;
            _aiStatusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _aiStatusTimer.Tick += (_, _) => UpdateAiStatus(_latestLlamaSnapshot);
            UpdateAiStatus(_latestLlamaSnapshot);

            ApplySettingsTtl();
        }

        public ICommand SendCommand { get; }
        public ICommand ExecuteProposedActionCommand { get; }
        public ICommand CopyMessageCommand { get; }

        public int DefaultTtlMs
        {
            get => _defaultTtlMs;
            set
            {
                var clamped = Math.Max(value, MinChatTtlMs);
                if (_defaultTtlMs == clamped)
                {
                    return;
                }

                _defaultTtlMs = clamped;
                OnPropertyChanged();
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                if (_inputText == value)
                {
                    return;
                }

                _inputText = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value)
                {
                    return;
                }

                _isBusy = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public string AiStatusText
        {
            get => _aiStatusText;
            private set
            {
                if (_aiStatusText == value)
                {
                    return;
                }

                _aiStatusText = value;
                OnPropertyChanged();
            }
        }

        public Brush AiStatusBrush
        {
            get => _aiStatusBrush;
            private set
            {
                if (Equals(_aiStatusBrush, value))
                {
                    return;
                }

                _aiStatusBrush = value;
                OnPropertyChanged();
            }
        }

        public bool IsAiLoading
        {
            get => _isAiLoading;
            private set
            {
                if (_isAiLoading == value)
                {
                    return;
                }

                _isAiLoading = value;
                OnPropertyChanged();
            }
        }

        public bool IsChatReady
        {
            get => _isChatReady;
            private set
            {
                if (_isChatReady == value)
                {
                    return;
                }

                _isChatReady = value;
                OnPropertyChanged();
                RaiseCanExecuteChanged();
            }
        }

        public Brush AiPillBackground { get; }

        public string? AiStatusTooltip
        {
            get => _aiStatusTooltip;
            private set
            {
                if (_aiStatusTooltip == value)
                {
                    return;
                }

                _aiStatusTooltip = value;
                OnPropertyChanged();
            }
        }

        private void OnLocalLlamaStateUpdated(object? sender, LocalLlamaStateSnapshot snapshot)
        {
            _dispatcher.Invoke(() => UpdateAiStatus(snapshot));
        }

        private void UpdateAiStatus(LocalLlamaStateSnapshot snapshot)
        {
            _latestLlamaSnapshot = snapshot;
            IsChatReady = snapshot.Status == LocalStatus.Ready;

            switch (snapshot.Status)
            {
                case LocalStatus.Ready:
                    AiStatusText = "État IA : Prête";
                    AiStatusBrush = ReadyBrush;
                    IsAiLoading = false;
                    AiStatusTooltip = null;
                    _aiStatusTimer.Stop();
                    break;
                case LocalStatus.Failed:
                case LocalStatus.Stopped:
                    AiStatusText = "État IA : Indisponible";
                    AiStatusBrush = FailedBrush;
                    IsAiLoading = false;
                    AiStatusTooltip = string.IsNullOrWhiteSpace(snapshot.LastFailure)
                        ? null
                        : $"Dernière erreur: {snapshot.LastFailure}";
                    _aiStatusTimer.Stop();
                    break;
                default:
                    AiStatusText = BuildLoadingStatus(snapshot);
                    AiStatusBrush = StartingBrush;
                    IsAiLoading = true;
                    AiStatusTooltip = null;
                    if (!_aiStatusTimer.IsEnabled)
                    {
                        _aiStatusTimer.Start();
                    }
                    break;
            }
        }

        private static string BuildLoadingStatus(LocalLlamaStateSnapshot snapshot)
        {
            if (snapshot.StartRequestedUtc.HasValue)
            {
                var elapsed = DateTimeOffset.UtcNow - snapshot.StartRequestedUtc.Value;
                if (elapsed.TotalSeconds > 60)
                {
                    return "État IA : Chargement… (lent)";
                }

                var seconds = Math.Max(0, (int)elapsed.TotalSeconds);
                return $"État IA : Chargement… ({seconds}s)";
            }

            return "État IA : Chargement…";
        }

        private void OnMessagePosted(string text, MessageType type, bool pinned, int? ttlMs)
        {
            var effectiveTtlMs = Math.Max(ttlMs ?? DefaultTtlMs, MinChatTtlMs);
            var item = new MessageItem
            {
                Text = text,
                Type = type,
                Pinned = pinned,
                Created = DateTime.Now,
                TtlMs = effectiveTtlMs,
                Role = "assistant"
            };

            _dispatcher.Invoke(() => Messages.Add(item));

            if (!item.Pinned)
            {
                var t = new Timer(item.TtlMs) { AutoReset = false };
                t.Elapsed += (_, __) =>
                {
                    _dispatcher.Invoke(() => AttemptExpireMessage(item));
                };
                t.Start();
            }
        }

        private async Task SendAsync()
        {
            if (!IsChatReady)
            {
                Log.Warn("Chat: tentative d'envoi alors que l'IA locale n'est pas prête.");
                return;
            }

            var message = InputText?.Trim();
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            InputText = string.Empty;

            var userItem = new MessageItem
            {
                Text = message,
                Type = MessageType.User,
                Created = DateTime.Now,
                Role = "user",
                Pinned = true,
                TtlMs = DefaultTtlMs
            };

            _dispatcher.Invoke(() => Messages.Add(userItem));
            _chat.RecordMessage("user", message);

            IsBusy = true;
            try
            {
                if (await TryRunLocalChatAsync(message).ConfigureAwait(false))
                {
                    return;
                }

                if (_assistantService is not null && _assistantContextProvider is not null && _actionExecutor is not null)
                {
                    var assistantContext = _assistantContextProvider();
                    var reply = await _assistantService.AskAsync(message, assistantContext).ConfigureAwait(false);
                    if (ShouldSuppressGenerationError(reply.Text))
                    {
                        Log.Warn($"Chat: suppression d'une erreur de génération pendant le warm-up: {reply.Text}");
                        return;
                    }

                    AppendAssistantReply(reply);
                    return;
                }

                if (_chatEngine is null || _actionBridge is null)
                {
                    _chat.PostSystemMessage("Aucun moteur de chat configuré", MessageType.Warning, ChatKind.Warning);
                    return;
                }

                var context = new Virgil.Services.Chat.ChatContext(_chat.Messages, "virgil");
                var result = await _chatEngine.GenerateAsync(message, context).ConfigureAwait(false);
                await _actionBridge.RouteAsync(result).ConfigureAwait(false);
            }
            catch (Virgil.Services.Chat.ChatEngineUnavailableException ex)
            {
                _chat.PostSystemMessage(ex.Message, MessageType.Warning, ChatKind.Warning);
            }
            catch (Exception ex)
            {
                _chat.PostSystemMessage($"Erreur chat: {ex.Message}", MessageType.Error, ChatKind.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool> TryRunLocalChatAsync(string message)
        {
            if (_settingsService is null)
            {
                return false;
            }

            var localState = LocalLlamaStateService.Instance;
            if (localState.Status != LocalStatus.Ready)
            {
                return false;
            }

            var baseUrl = string.IsNullOrWhiteSpace(_settingsService.Settings.EmbeddedLlamaBaseUrl)
                ? localState.BaseUrl
                : _settingsService.Settings.EmbeddedLlamaBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return false;
            }

            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl, UriKind.Absolute),
                Timeout = TimeSpan.FromSeconds(_settingsService.Settings.EmbeddedLlamaTimeoutSeconds)
            };
            LocalLlamaHttpClientConfigurator.ConfigureAuthHeaders(client, _settingsService.Settings.EmbeddedLlamaApiKey);

            var llamaClient = new LocalLlamaClient(client);
            var probe = await llamaClient.ChatAsync(message, _settingsService.Settings.LocalMaxTokens).ConfigureAwait(false);
            if (probe.Success)
            {
                var reply = string.IsNullOrWhiteSpace(probe.Content) ? "Réponse vide." : probe.Content;
                _chat.PostSystemMessage(reply, MessageType.Info, ChatKind.Info);
                return true;
            }

            var errorMessage = string.IsNullOrWhiteSpace(probe.ErrorMessage)
                ? "Erreur génération: réponse vide."
                : probe.ErrorMessage;
            if (ShouldSuppressGenerationError(errorMessage))
            {
                Log.Warn($"Chat: suppression d'une erreur de génération pendant le warm-up: {errorMessage}");
                return true;
            }

            _chat.PostSystemMessage(errorMessage, MessageType.Warning, ChatKind.Warning);
            return true;
        }

        private void AppendAssistantReply(AssistantReply reply)
        {
            var item = new MessageItem
            {
                Text = reply.Text,
                Type = MessageType.Info,
                Pinned = true,
                Created = DateTime.Now,
                TtlMs = DefaultTtlMs,
                Role = "assistant",
                ProposedActions = (reply.ProposedActions ?? Array.Empty<ProposedAction>())
                    .Select(action => new ProposedActionItem(
                        action.ActionId,
                        action.Title,
                        action.Parameters is null ? null : new Dictionary<string, string>(action.Parameters),
                        action.Warning))
                    .ToList()
            };

            _dispatcher.Invoke(() => Messages.Add(item));
            _chat.RecordMessage("assistant", reply.Text);
        }

        private async Task ExecuteProposedActionAsync(object? parameter)
        {
            if (parameter is not ProposedActionItem action || _actionExecutor is null)
            {
                return;
            }

            AppendAssistantMessage($"Exécution… {action.Title}", MessageType.Info);

            try
            {
                var result = await _actionExecutor(action.ActionId, action.Parameters, CancellationToken.None).ConfigureAwait(false);
                var summary = string.IsNullOrWhiteSpace(result.Message) ? result.Title : result.Message;
                AppendAssistantMessage(summary, result.Success ? MessageType.Success : MessageType.Warning);
            }
            catch (Exception ex)
            {
                AppendAssistantMessage($"Erreur pendant {action.Title} : {ex.Message}", MessageType.Error);
            }
        }

        private void AppendAssistantMessage(string text, MessageType type)
        {
            var item = new MessageItem
            {
                Text = text,
                Type = type,
                Pinned = true,
                Created = DateTime.Now,
                TtlMs = DefaultTtlMs,
                Role = "assistant"
            };

            _dispatcher.Invoke(() => Messages.Add(item));
            _chat.RecordMessage("assistant", text);
        }

        private void AttemptExpireMessage(MessageItem item)
        {
            if (item.Pinned || item.IsExpired || !Messages.Contains(item))
            {
                return;
            }

            if (IsBusy)
            {
                var retry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ExpireRetrySeconds) };
                retry.Tick += (_, __) =>
                {
                    retry.Stop();
                    AttemptExpireMessage(item);
                };
                retry.Start();
                return;
            }

            item.IsExpired = true;
            OnPropertyChanged(nameof(Messages));
            var remover = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            remover.Tick += (_, __) =>
            {
                remover.Stop();
                Messages.Remove(item);
            };
            remover.Start();
        }

        private void ApplySettingsTtl()
        {
            if (_settingsService is null)
            {
                DefaultTtlMs = MinChatTtlMs;
                return;
            }

            var ttlSeconds = Math.Max(_settingsService.Settings.ChatMessageTTLSeconds, MinChatTtlMs / 1000);
            DefaultTtlMs = ttlSeconds * 1000;
            _chat.AutoEraseDelay = TimeSpan.FromSeconds(ttlSeconds);
        }

        private void CopyMessage(MessageItem? item)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.Text))
            {
                return;
            }

            try
            {
                System.Windows.Clipboard.SetText(item.Text);
            }
            catch
            {
            }
        }

        private bool CanSend() => IsChatReady && !IsBusy && !string.IsNullOrWhiteSpace(InputText);

        private static bool ShouldSuppressGenerationError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            if (LocalLlamaStateService.Instance.Status != LocalStatus.Starting)
            {
                return false;
            }

            return message.Contains("Erreur génération", StringComparison.OrdinalIgnoreCase);
        }

        private void RaiseCanExecuteChanged()
        {
            if (SendCommand is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
