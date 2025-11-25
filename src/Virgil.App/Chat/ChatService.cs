using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    /// <summary>
    /// Default chat pipeline implementation for the Virgil application.
    /// In this configuration the chat view is read-only: only Virgil
    /// produces messages, the user does not type. Other parts of the
    /// application can push messages through the PostSystemMessage helper.
    /// </summary>
    public partial class ChatService : IChatService
    {
        private readonly List<ChatMessage> _messages = new();

        // Very small built-in panel of default phrases Virgil can use when
        // he wants to comment the system state or fill the silence. This can
        // later be replaced or extended by data coming from JSON or another
        // domain service.
        private static readonly string[] _defaultPhrases = new[]
        {
            "Je garde un oeil sur le système.",
            "Tout est calme pour l’instant.",
            "Je reste en veille, prêt à intervenir.",
            "Scan en cours… rien à signaler.",
            "Je surveille les process, tranquille."
        };

        private static readonly Random _random = new();

        /// <inheritdoc />
        public IReadOnlyList<ChatMessage> Messages => _messages;

        /// <inheritdoc />
        public async Task<ChatMessage> SendAsync(string content, CancellationToken cancellationToken = default)
        {
            // In the current Virgil-only chat model there is no direct user
            // input, but this method remains available for compatibility.
            var assistantMessage = new ChatMessage("assistant", content);
            _messages.Add(assistantMessage);

            await Task.CompletedTask;
            return assistantMessage;
        }

        /// <summary>
        /// Adds a new assistant/system message to the conversation without
        /// any notion of user input. This is the main entry point for parts
        /// of the application that want Virgil to "speak" in the chat box.
        /// </summary>
        public void PostSystemMessage(string content, MessageType type = MessageType.Info, ChatKind kind = ChatKind.Info)
        {
            // For now we only persist the textual content. The kind/type can
            // be leveraged later to drive styling or routing.
            var message = new ChatMessage("assistant", content);
            _messages.Add(message);
        }

        /// <summary>
        /// Picks a random phrase from Virgil's default panel and posts it as
        /// a system/assistant message. This is the building block for the
        /// small talk / ambient commentary behaviour.
        /// </summary>
        public void PostRandomPhrase()
        {
            if (_defaultPhrases.Length == 0)
            {
                return;
            }

            var content = _defaultPhrases[_random.Next(_defaultPhrases.Length)];
            PostSystemMessage(content, MessageType.Info, ChatKind.Info);
        }
    }
}
