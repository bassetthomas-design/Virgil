using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    public partial class ChatService
    {
        private readonly ObservableCollection<ChatMessage> _messages = new();
        public ReadOnlyObservableCollection<ChatMessage> MessagesRO => new ReadOnlyObservableCollection<ChatMessage>(_messages);

        // ... existing members remain ...
    }
}
