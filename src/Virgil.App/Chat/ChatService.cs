using System;
using System.Timers;
namespace Virgil.App.Chat
{
    public class ChatService
    {
        public event Action<string, MessageType, bool, int?>? MessagePosted;

        // Base overload
        public void Post(string message) => MessagePosted?.Invoke(message, MessageType.Info, false, null);

        // Variadic for backward-compat calls
        public void Post(string message, params object[] args) => MessagePosted?.Invoke(message, MessageType.Info, false, null);

        // MVP signature with type/pinned/ttl
        public void Post(string message, MessageType type, bool pinned = false, int? ttlMs = null)
            => MessagePosted?.Invoke(message, type, pinned, ttlMs);
    }
}
