using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    /// <summary>
    /// Default chat pipeline implementation for the Virgil application.
    /// This version implements the shared IChatService contract so that
    /// higher layers can depend on the abstraction defined in Core while
    /// the internal behaviour continues to evolve.
    /// Other aspects of the behaviour (such as Thanos / history clearing)
    /// live in partial declarations of this class.
    /// </summary>
    public partial class ChatService : IChatService
    {
        /// <inheritdoc />
        public async Task<ChatMessage> SendAsync(string content, CancellationToken cancellationToken = default)
        {
            // Basic placeholder implementation to keep the dev branch build
            // green while the full AI/chat pipeline is being wired.
            var assistantMessage = new ChatMessage("assistant", string.Empty);
            await Task.CompletedTask;
            return assistantMessage;
        }
    }
}
