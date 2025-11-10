using Virgil.App.Chat;
using Virgil.App.Services;

namespace Virgil.App.ViewModels;

public sealed class MainViewModel
{
    public ChatService Chat { get; }
    public PhraseEngine Phrases { get; }
    public MonitoringViewModel Monitoring { get; }

    public MainViewModel()
    {
        Chat = new ChatService();
        Phrases = new PhraseEngine();
        Monitoring = new MonitoringViewModel(new MonitoringService());
    }

    public void Say(string text) => Chat.Post(text);
    public void Progress(string label, int percent) => Chat.Post($"{label} {percent}%", ChatKind.Progress, percent);
}
