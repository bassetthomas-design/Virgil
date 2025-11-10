namespace Virgil.App.Chat;

public enum ChatKind { Info, Progress, Success, Warning, Error }

public sealed class ChatMessage
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public TimeSpan Ttl { get; init; } = TimeSpan.FromSeconds(60);
    public string Text { get; init; } = string.Empty;
    public ChatKind Kind { get; init; } = ChatKind.Info;
    public int? Progress { get; init; }
}
