using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel
    {
        // Match: MessagePostedHandler(object sender, string text, ChatKind kind, int? ttlMs)
        private void OnMessagePosted(object sender, string text, ChatKind kind, int? ttlMs)
        {
                   MessageType messageType = kind switch
            {
                ChatKind.Success => MessageType.Success,
                ChatKind.Warning => MessageType.Warning,
                ChatKind.Error => MessageType.Error,
                ChatKind.Info => MessageType.Info,
                _ => MessageType.Info
            };
            OnMessagePosted(text, messageType, false, ttlMs);

        }
    }
}
