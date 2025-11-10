using System.Collections.ObjectModel;
using System.Timers;

namespace Virgil.App.Chat;

public sealed class ChatService : IDisposable
{
    public ObservableCollection<ChatMessage> Messages { get; } = new();
    private readonly Timer _gc = new(1000);

    public ChatService()
    {
        _gc.Elapsed += (_, _) => Cleanup();
        _gc.Start();
    }

    public void Post(string text, ChatKind kind = ChatKind.Info, int? progress = null, int ttlSeconds = 60)
    {
        var msg = new ChatMessage { Text = text, Kind = kind, Progress = progress, Ttl = TimeSpan.FromSeconds(ttlSeconds) };
        App.Current?.Dispatcher.Invoke(() => Messages.Add(msg));
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        App.Current?.Dispatcher.Invoke(() =>
        {
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                var m = Messages[i];
                if (now - m.CreatedAt >= m.Ttl) Messages.RemoveAt(i);
            }
        });
    }

    public void Dispose() => _gc.Dispose();
}
