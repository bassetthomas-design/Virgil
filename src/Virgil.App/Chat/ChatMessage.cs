using System;

namespace Virgil.App.Chat
{
    public enum MessageType { Info, Warning, Error, Success }
    public enum ChatKind    { Info, Warning, Error, Success }

    public class ChatMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Role { get; set; } = "system";
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public TimeSpan? Ttl { get; set; }
        public MessageType Type { get; set; } = MessageType.Info;
    }
}
