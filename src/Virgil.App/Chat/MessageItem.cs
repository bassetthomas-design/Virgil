using System;

namespace Virgil.App.Chat
{
    public class MessageItem
    {
        public string Text { get; set; } = string.Empty;
        public MessageType Type { get; set; } = MessageType.Info;
        public bool Pinned { get; set; }
        public DateTime Created { get; set; }
        public int TtlMs { get; set; } = 60000;
        public bool IsExpired { get; set; }
    }
}
