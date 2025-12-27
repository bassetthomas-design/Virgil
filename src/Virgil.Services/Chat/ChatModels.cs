using System.Collections.Generic;
using Virgil.App.Chat;

namespace Virgil.Services.Chat;

public enum ChatCommandType
{
    None,
    Action
}

public sealed record ChatCommand(ChatCommandType Type, string? Action = null, string? Raw = null)
{
    public static ChatCommand None { get; } = new(ChatCommandType.None);
}

public sealed record ChatEngineResult(string Text, ChatCommand Command)
{
    public static ChatEngineResult Empty { get; } = new(string.Empty, ChatCommand.None);
}

public sealed record ChatContext(IReadOnlyList<ChatMessage> History, string SystemPrompt);

public interface IChatEngine
{
    Task<ChatEngineResult> GenerateAsync(string userText, ChatContext context, CancellationToken ct = default);
}

public sealed class ChatEngineUnavailableException : Exception
{
    public ChatEngineUnavailableException(string message) : base(message)
    {
    }
}
