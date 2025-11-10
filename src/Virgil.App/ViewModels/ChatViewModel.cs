using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Timer = System.Timers.Timer;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MessageItem> Messages { get; } = new();
        private readonly ChatService _chat;

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

            Messages.Add(item);

            if (!item.Pinned)
            {
                var t = new Timer(item.TtlMs) { AutoReset = false };
                t.Elapsed += (_, __) =>
                {
                    // Marque comme expiré (l'UI appliquera l'effet Thanos et retirera l'élément)
                    item.IsExpired = true;
                    OnPropertyChanged(nameof(Messages));
                };
                t.Start();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
