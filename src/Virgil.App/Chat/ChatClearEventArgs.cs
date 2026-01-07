using System;

namespace Virgil.App.Chat
{
    /// <summary>
    /// Event args used when the chat history is cleared, optionally with
    /// a progressive Thanos effect.
    /// </summary>
    public sealed class ChatClearEventArgs : EventArgs
    {
        public ChatClearEventArgs(bool applyEffect, int effectDurationMs, bool animateOverlay = false)
        {
            ApplyEffect = applyEffect;
            EffectDurationMs = effectDurationMs;
            AnimateOverlay = animateOverlay;
        }

        /// <summary>
        /// Gets a value indicating whether the Thanos effect should be applied.
        /// </summary>
        public bool ApplyEffect { get; }

        /// <summary>
        /// Gets the duration of the effect in milliseconds.
        /// </summary>
        public int EffectDurationMs { get; }

        /// <summary>
        /// Gets a value indicating whether the UI should animate a snapshot overlay.
        /// </summary>
        public bool AnimateOverlay { get; }
    }
}
