using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel
    {
        public event Func<int, Task>? SnapRequested;

        public void SnapAll() => _ = SnapAllAsync();

        public async Task SnapAllAsync()
        {
            await _chat.ClearAllAsync(applyThanosEffect: true).ConfigureAwait(false);
        }

        private void OnHistoryCleared(object? sender, ChatClearEventArgs e)
        {
            _ = _dispatcher.InvokeAsync(async () =>
            {
                if (!e.ApplyEffect)
                {
                    Messages.Clear();
                    return;
                }

                if (e.AnimateOverlay && SnapRequested is not null)
                {
                    try
                    {
                        await SnapRequested.Invoke(e.EffectDurationMs);
                    }
                    catch (Exception)
                    {
                        // Ignore animation errors to ensure cleanup completes.
                    }
                }

                Messages.Clear();
            }, DispatcherPriority.Background);
        }
    }
}
