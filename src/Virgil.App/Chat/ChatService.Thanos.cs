using System.Collections.ObjectModel;

namespace Virgil.App.Chat
{
    public partial class ChatService
    {
        public ObservableCollection<ChatMessage> Messages => _messages;
        public void ClearAll(){
            if(_messages == null) return;
            _messages.Clear();
            // Optionnel: notifier un message systeme "purge" si besoin
        }
    }
}
