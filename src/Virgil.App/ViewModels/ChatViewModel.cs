using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel : ObservableObject
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
                TtlMs = ttlMs ?? DefaultTtlMs
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
    }
}
