using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Windows.Threading;
using Virgil.App.Chat;
using Virgil.App.Commands;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MessageItem> Messages { get; } = new();
        private readonly ChatService _chat;
        private readonly Virgil.Services.Chat.ChatActionBridge? _actionBridge;
        private readonly Virgil.Services.Chat.IChatEngine? _chatEngine;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private string _inputText = string.Empty;
        private bool _isBusy;

        public ChatViewModel(ChatService chat, Virgil.Services.Chat.ChatActionBridge? bridge = null, Virgil.Services.Chat.IChatEngine? engine = null)
        {
            _chat = chat;
            _actionBridge = bridge;
            _chatEngine = engine;
            _chat.MessagePosted += OnMessagePosted;
            _chat.HistoryCleared += OnHistoryCleared;
            SendCommand = new RelayCommand(_ => _ = SendAsync(), _ => CanSend());
        }

        public ICommand SendCommand { get; }

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

            IsBusy = true;
            try
            {
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
