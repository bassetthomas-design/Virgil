using System;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    public partial class ChatService
    {
        public event EventHandler? MessagePosted;

        public Task Post(string text)
        {
            MessagePosted?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task Post(string role, string text)
        {
            MessagePosted?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
