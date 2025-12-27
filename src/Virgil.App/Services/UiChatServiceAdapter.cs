using System.Threading;
using System.Threading.Tasks;
using Virgil.App.Chat;
using Virgil.App.Utils;
using Virgil.Services.Abstractions;

namespace Virgil.App.Services
{
    public sealed class UiChatServiceAdapter : IChatService
    {
        private readonly ChatService _chatService;

        public UiChatServiceAdapter(ChatService chatService)
        {
            _chatService = chatService;
        }

        public Task InfoAsync(string message, CancellationToken ct = default)
        {
            StartupLog.Write(message);
            _chatService.PostSystemMessage(message, MessageType.Info, ChatKind.Info);
            return Task.CompletedTask;
        }

        public Task WarnAsync(string message, CancellationToken ct = default)
        {
            StartupLog.Write(message);
            _chatService.PostSystemMessage(message, MessageType.Warning, ChatKind.Warning);
            return Task.CompletedTask;
        }

        public Task ErrorAsync(string message, CancellationToken ct = default)
        {
            StartupLog.Write(message);
            _chatService.PostSystemMessage(message, MessageType.Error, ChatKind.Error);
            return Task.CompletedTask;
        }

        public Task ThanosWipeAsync(bool preservePinned = true, CancellationToken ct = default)
        {
            return _chatService.ClearHistoryAsync(applyThanosEffect: true, ct: ct);
        }
    }
}
