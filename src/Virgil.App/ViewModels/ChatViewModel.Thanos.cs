using System.Collections.ObjectModel;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel
    {
        public ObservableCollection<ChatMessage> Messages => Chat.Messages;
        public void SnapAll(){ Chat.ClearAll(); }
    }
}
