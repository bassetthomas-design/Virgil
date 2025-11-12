using System.Threading.Tasks;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class MainViewModel
    {
        public Task Say(string text) => Task.CompletedTask;
        public Task Say(string text, MessageType type, bool pinned = false, int? ttlMs = null) => Task.CompletedTask;
        public void Progress(int percent, string? text = null) { /* TODO: wire to UI progress */ }
        public void Progress(string text, int percent) => Progress(percent, text);
    }
}
