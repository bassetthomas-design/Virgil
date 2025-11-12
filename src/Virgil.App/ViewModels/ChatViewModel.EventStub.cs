using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel
    {
        // Match: MessagePostedHandler(object sender, string text, ChatKind kind, int? ttlMs)
        private void OnMessagePosted(object sender, string text, ChatKind kind, int? ttlMs)
        {
            // TODO: update UI state if needed
        }
    }
}
