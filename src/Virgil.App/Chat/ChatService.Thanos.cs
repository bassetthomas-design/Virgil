using System;
using System.Threading;
using System.Threading.Tasks;

namespace Virgil.App.Chat
{
    /// <summary>
    /// Implements the Thanos snap behaviour for the chat service. This partial adds
    /// methods to clear the chat history with a progressive deletion effect. It
    /// uses the MessagePosted event to notify the UI as messages disappear.
    /// </summary>
    public partial class ChatService
    {
        private bool _snapInProgress;

        /// <summary>
        /// Clears the entire chat history. If <paramref name="applyThanosEffect"/> is true,
        /// messages are removed one by one over <paramref name="effectDurationMs"/> milliseconds
        /// to simulate a disintegration effect. Otherwise they are cleared instantly.
        /// </summary>
        /// <param name="applyThanosEffect">Whether to play the snap animation.</param>
        /// <param name="effectDurationMs">Total duration of the effect in milliseconds.</param>
        /// <param name="ct">Cancellation token to abort the operation.</param>
        public async Task ClearHistoryAsync(bool applyThanosEffect = false, int effectDurationMs = 2000, CancellationToken ct = default)
        {
            if (applyThanosEffect)
            {
                await SnapAsync(effectDurationMs, ct).ConfigureAwait(false);
            }
            else
            {
                lock (_messages)
                {
                    _messages.Clear();
                }
            }

            // Notify listeners (UI, logging) that the history has been wiped.
            MessagePosted?.Invoke(this, "[Chat effacé]", ChatKind.Info, null);
            HistoryCleared?.Invoke(this, new ChatClearEventArgs(applyThanosEffect, effectDurationMs));
        }

        /// <summary>
        /// Public alias kept for TODO tracking compatibility. Invokes
        /// <see cref="ClearHistoryAsync(bool, int, CancellationToken)"/>.
        /// </summary>
        public Task ClearAllAsync(bool applyThanosEffect = true, int effectDurationMs = 2000, CancellationToken ct = default)
            => ClearHistoryAsync(applyThanosEffect, effectDurationMs, ct);

        /// <summary>
        /// Gradually removes messages to produce a "snap" effect. The delay between
        /// deletions is determined by the number of messages and the total duration.
        /// </summary>
        /// <param name="durationMs">Total effect duration in milliseconds.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task SnapAsync(int durationMs = 2000, CancellationToken ct = default)
        {
            if (_snapInProgress) return;
            _snapInProgress = true;
            int count;
            lock (_messages)
            {
                count = _messages.Count;
            }
            if (count <= 0)
            {
                _snapInProgress = false;
                return;
            }
            // Compute delay per message
            int delay = durationMs / Math.Max(1, count);
            for (int i = count - 1; i >= 0; i--)
            {
                ct.ThrowIfCancellationRequested();
                lock (_messages)
                {
                    if (i < _messages.Count)
                    {
                        _messages.RemoveAt(i);
                    }
                }
                // Fire an event for each removal so that the UI can animate the bubble
                MessagePosted?.Invoke(this, "[Message supprimé]", ChatKind.Info, null);
                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
            _snapInProgress = false;
        }
    }
}
