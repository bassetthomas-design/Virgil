using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Timer = System.Timers.Timer;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MessageItem> Messages { get; } = new();
        private readonly ChatService _chat;
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        public ChatViewModel(ChatService chat)
        {
            _chat = chat;
            _chat.MessagePosted += OnMessagePosted;
        }

        private void OnMessagePosted(string text, MessageType type, bool pinned, int? ttlMs)
        {
            var item = new MessageItem
            {
                Text = text,
                Type = type,
                Pinned = pinned,
                Created = DateTime.Now,
                TtlMs = ttlMs ?? 60000
            };

            _dispatcher.Invoke(() => Messages.Add(item));

            if (!item.Pinned)
            {
                var t = new Timer(item.TtlMs) { AutoReset = false };
                t.Elapsed += (_, __) =>
                {
                    // Marque expiré pour animer, puis supprime après 650ms
                    _dispatcher.Invoke(() =>
                    {
                        item.IsExpired = true;
                        OnPropertyChanged(nameof(Messages));
                        var remover = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
