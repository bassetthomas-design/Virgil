using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel
    {
        // Remove invalid static return type usage. If PhraseEngine methods are needed, call them directly:
        public void SeedWelcomeMessage(ChatService chat)
        {
            // Example: chat.Post(PhraseEngine.Welcome()); // assuming static method exists
        }
    }
}
