using System;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    // 4-arg handler expected by existing subscribers
    public delegate void MessagePostedHandler(object sender, string text, ChatKind kind, int? ttlMs);

    public partial class ChatService
    {
        public event MessagePostedHandler? MessagePosted;

        // Basic posts
        public Task Post(string text)
        {
            var message = new ChatMessage("assistant", text);
            _messages.Add(message);
            MessagePosted?.Invoke(this, text, ChatKind.Info, null);
            return Task.CompletedTask;
        }

        // Overloads with ChatKind
        public Task Post(string text, ChatKind kind, bool pinned = false, int? ttlMs = null)
        {
            var message = new ChatMessage("assistant", text);
            _messages.Add(message);
            MessagePosted?.Invoke(this, text, kind, ttlMs);
            return Task.CompletedTask;
        }

        public Task Post(string role, string text)
        {
            var message = new ChatMessage(role, text);
            _messages.Add(message);
            MessagePosted?.Invoke(this, text, ChatKind.Info, null);
            return Task.CompletedTask;
        }

        public Task Post(string role, string text, ChatKind kind, bool pinned = false, int? ttlMs = null)
        {
            var message = new ChatMessage(role, text);
            _messages.Add(message);
            MessagePosted?.Invoke(this, text, kind, ttlMs);
            return Task.CompletedTask;
        }

        // Overloads with MessageType kept for compatibility (map to ChatKind)
        public Task Post(string text, MessageType type, bool pinned = false, int? ttlMs = null)
        {
            var kind = type == MessageType.Error ? ChatKind.Error : type == MessageType.Warning ? ChatKind.Warning : ChatKind.Info;
            var message = new ChatMessage("assistant", text);
            _messages.Add(message);
            MessagePosted?.Invoke(this, text, kind, ttlMs);
            return Task.CompletedTask;
        }

        public Task Post(string role, string text, MessageType type, bool pinned = false, int? ttlMs = null)
        {
            var kind = type == MessageType.Error ? ChatKind.Error : type == MessageType.Warning ? ChatKind.Warning : ChatKind.Info;
            var message = new ChatMessage(role, text);
            _messages.Add(message);
            MessagePosted?.Invoke(this, text, kind, ttlMs);
            return Task.CompletedTask;
        }
    }
}
