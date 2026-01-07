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
        private readonly object _thanosTimerLock = new();
        private CancellationTokenSource? _thanosTimerCts;
        private int _activityVersion;

        internal TimeSpan AutoEraseDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Clears the entire chat history. If <paramref name="applyThanosEffect"/> is true,
        /// the UI animates a snapshot while the history is cleared after
        /// <paramref name="effectDurationMs"/> milliseconds. Otherwise they are cleared instantly.
        /// </summary>
        /// <param name="applyThanosEffect">Whether to play the snap animation.</param>
        /// <param name="effectDurationMs">Total duration of the effect in milliseconds.</param>
        /// <param name="ct">Cancellation token to abort the operation.</param>
        public async Task ClearHistoryAsync(bool applyThanosEffect = false, int effectDurationMs = 800, CancellationToken ct = default, bool startAutoEraseTimer = true)
        {
            var generation = RegisterActivity(rearmTimer: false);

            if (applyThanosEffect)
            {
                // Trigger the UI overlay animation before clearing the data store.
                HistoryCleared?.Invoke(this, new ChatClearEventArgs(applyThanosEffect, effectDurationMs, animateOverlay: true));
                await SnapAsync(effectDurationMs, ct).ConfigureAwait(false);
            }
            else
            {
                lock (_messages)
                {
                    _messages.Clear();
                }

                // Notify listeners (UI, logging) that the history has been wiped.
                HistoryCleared?.Invoke(this, new ChatClearEventArgs(applyThanosEffect, effectDurationMs));
            }

            PostSystemMessage("Tout a disparu.", MessageType.Info, ChatKind.Info, rearmTimer: false);

            if (startAutoEraseTimer)
            {
                ArmThanosTimer(generation);
            }
        }

        /// <summary>
        /// Public alias kept for TODO tracking compatibility. Invokes
        /// <see cref="ClearHistoryAsync(bool, int, CancellationToken)"/>.
        /// </summary>
        public Task ClearAllAsync(bool applyThanosEffect = true, int effectDurationMs = 800, CancellationToken ct = default)
            => ClearHistoryAsync(applyThanosEffect, effectDurationMs, ct);

        /// <summary>
        /// Delays the actual clear operation so the UI can animate the snapshot overlay.
        /// </summary>
        /// <param name="durationMs">Total effect duration in milliseconds.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task SnapAsync(int durationMs = 800, CancellationToken ct = default)
        {
            if (_snapInProgress) return;
            _snapInProgress = true;
            try
            {
                if (durationMs > 0)
                {
                    await Task.Delay(durationMs, ct).ConfigureAwait(false);
                }
                lock (_messages)
                {
                    _messages.Clear();
                }
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                _snapInProgress = false;
            }
        }

        private void ArmThanosTimer(int generation)
        {
            lock (_thanosTimerLock)
            {
                _thanosTimerCts?.Cancel();
                _thanosTimerCts?.Dispose();
                _thanosTimerCts = new CancellationTokenSource();
                var token = _thanosTimerCts.Token;
                _ = RunAutoEraseAsync(generation, token);
            }
        }

        private async Task RunAutoEraseAsync(int generation, CancellationToken token)
        {
            try
            {
                await Task.Delay(AutoEraseDelay, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (generation != Volatile.Read(ref _activityVersion))
            {
                return;
            }

            await ClearHistoryAsync(applyThanosEffect: true, effectDurationMs: 800, ct: token, startAutoEraseTimer: false).ConfigureAwait(false);
        }

        private int RegisterActivity(bool rearmTimer = true)
        {
            var generation = Interlocked.Increment(ref _activityVersion);
            if (rearmTimer)
            {
                ArmThanosTimer(generation);
            }

            return generation;
        }
    }
}
