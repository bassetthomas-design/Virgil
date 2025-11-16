using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    /// <summary>
    /// Abstraction for the Virgil chat pipeline. This is a minimal surface
    /// extracted from the existing ChatService so that higher layers can
    /// depend on an interface instead of a concrete implementation.
    /// </summary>
    public interface IChatService
    {
        /// <summary>
        /// Gets the messages currently in the conversation history.
        /// </summary>
        IReadOnlyList<ChatMessage> Messages { get; }

        /// <summary>
        /// Sends a user message and returns the assistant response once available.
        /// Implementations may stream tokens internally but expose the final
        /// composed response here.
        /// </summary>
        Task<ChatMessage> SendAsync(string content, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Lightweight DTO representing a chat message in the Virgil conversation.
    /// This mirrors the shape currently used by the App chat layer so that it
    /// can be shared across Core, Services and UI.
    /// </summary>
    public record ChatMessage(string Role, string Content);
}
