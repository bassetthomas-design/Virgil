using System.Threading.Tasks;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class ActionsViewModel
    {
        private readonly ChatService _chat;
        public ActionsViewModel(ChatService chat){ _chat = chat; }

        public async Task NotifySetupAsync(){ await _chat.Post("Configuration terminée", MessageType.Info, ttlMs: 2500); }
        public async Task NotifyCleanupAsync(){ await _chat.Post("Nettoyage terminé", MessageType.Info, ttlMs: 2500); }
        public async Task NotifyDriversOkAsync(){ await _chat.Post("Pilotes OK", MessageType.Info, ttlMs: 2500); }
        public async Task NotifyAppsOkAsync(){ await _chat.Post("Applications OK", MessageType.Info, ttlMs: 2500); }
        public async Task NotifySystemOkAsync(){ await _chat.Post("Système OK", MessageType.Info, ttlMs: 2500); }
    }
}
