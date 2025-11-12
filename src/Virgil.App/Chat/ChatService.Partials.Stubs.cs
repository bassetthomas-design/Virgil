using System;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    public delegate void MessagePostedHandler(object sender, string text, MessageType type, bool pinned, int? ttlMs);

    public partial class ChatService
    {
        public event MessagePostedHandler? MessagePosted;

        public Task Post(string text)
        {
            MessagePosted?.Invoke(this, text, MessageType.Info, false, null);
            return Task.CompletedTask;
        }

        public Task Post(string text, MessageType type, bool pinned = false, int? ttlMs = null)
        {
            MessagePosted?.Invoke(this, text, type, pinned, ttlMs);
            return Task.CompletedTask;
        }

        public Task Post(string role, string text)
        {
            MessagePosted?.Invoke(this, text, MessageType.Info, false, null);
            return Task.CompletedTask;
        }

        public Task Post(string role, string text, MessageType type, bool pinned = false, int? ttlMs = null)
        {
            MessagePosted?.Invoke(this, text, type, pinned, ttlMs);
            return Task.CompletedTask;
        }
    }
}
