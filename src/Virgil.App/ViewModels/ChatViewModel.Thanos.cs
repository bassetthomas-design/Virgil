using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Virgil.App.Chat;

namespace Virgil.App.ViewModels
{
    public partial class ChatViewModel
    {
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

                var removable = Messages.Where(m => !m.Pinned).Reverse().ToList();
                if (removable.Count == 0)
                {
                    return;
                }

                int delay = e.EffectDurationMs / Math.Max(1, removable.Count);
                foreach (var item in removable)
                {
                    item.IsExpired = true;
                    OnPropertyChanged(nameof(Messages));

                    var remover = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
                    remover.Tick += (_, __) =>
                    {
                        remover.Stop();
                        Messages.Remove(item);
                    };
                    remover.Start();

                    await Task.Delay(delay).ConfigureAwait(false);
                }
            }, DispatcherPriority.Background);
        }
    }
}
