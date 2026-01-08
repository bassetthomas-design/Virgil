using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Windows.Threading;
using Virgil.App.Chat;
using Virgil.App.Commands;
using Virgil.App.Models;
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
        private readonly Func<AssistantContext>? _assistantContextProvider;
        private readonly Func<string, Dictionary<string, string>?, CancellationToken, Task<ActionResult>>? _actionExecutor;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private string _inputText = string.Empty;
        private bool _isBusy;
        private int _defaultTtlMs = 60000;

        public ChatViewModel(
            ChatService chat,
            Virgil.Services.Chat.ChatActionBridge? bridge = null,
            Virgil.Services.Chat.IChatEngine? engine = null,
            IAssistantService? assistantService = null,
            Func<AssistantContext>? assistantContextProvider = null,
            Func<string, Dictionary<string, string>?, CancellationToken, Task<ActionResult>>? actionExecutor = null)
        {
            _chat = chat;
            _actionBridge = bridge;
            _chatEngine = engine;
            _assistantService = assistantService;
            _assistantContextProvider = assistantContextProvider;
            _actionExecutor = actionExecutor;
            _chat.MessagePosted += OnMessagePosted;
            _chat.HistoryCleared += OnHistoryCleared;
            SendCommand = new RelayCommand(_ => _ = SendAsync(), _ => CanSend());
            ExecuteProposedActionCommand = new AsyncRelayCommand(ExecuteProposedActionAsync);
        }

        public ICommand SendCommand { get; }
        public ICommand ExecuteProposedActionCommand { get; }

        public int DefaultTtlMs
        {
            get => _defaultTtlMs;
            set
            {
                if (_defaultTtlMs == value)
                {
                    return;
                }

                _defaultTtlMs = value;
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

        private void OnMessagePosted(string text, MessageType type, bool pinned, int? ttlMs)
        {
            var item = new MessageItem
            {
                Text = text,
                Type = type,
                Pinned = pinned,
                Created = DateTime.Now,
                TtlMs = ttlMs ?? DefaultTtlMs,
                Role = "assistant"
            };

            _dispatcher.Invoke(() => Messages.Add(item));

            if (!item.Pinned)
            {
                var t = new Timer(item.TtlMs) { AutoReset = false };
                t.Elapsed += (_, __) =>
                {
                    _dispatcher.Invoke(() =>
                    {
                        item.IsExpired = true;
                        OnPropertyChanged(nameof(Messages));
                        var remover = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                        remover.Tick += (_, __) =>
                        {
                            remover.Stop();
                            Messages.Remove(item);
                        };
                        remover.Start();
                    });
                };
                t.Start();
            }
        }

        private async Task SendAsync()
        {
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
                if (_assistantService is not null && _assistantContextProvider is not null && _actionExecutor is not null)
                {
                    var assistantContext = _assistantContextProvider();
                    var reply = await _assistantService.AskAsync(message, assistantContext).ConfigureAwait(false);
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
            catch (Exception ex)
            {
                _chat.PostSystemMessage($"Erreur chat: {ex.Message}", MessageType.Error, ChatKind.Error);
            }
            finally
            {
                IsBusy = false;
            }
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

        private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

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
