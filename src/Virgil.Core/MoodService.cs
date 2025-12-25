using System;
using AvatarMood = Virgil.Core.Mood;

namespace Virgil.Core
{
    /// <summary>
    /// Provides the current mood and an associated colour. The mood can be
    /// updated externally and will raise an event when it changes. Additional
    /// moods such as Excited and Anxious have been added to enrich the
    /// emotional range of Virgil (see issue #150).
    /// </summary>
    public class MoodService
    {
        private AvatarMood _currentMood = AvatarMood.Neutral;
        /// <summary>
        /// Fires when the mood changes.
        /// </summary>
        public event EventHandler? MoodChanged;
        /// <summary>
        /// Gets or sets the current mood. Setting a different mood raises the
        /// <see cref="MoodChanged"/> event.
        /// </summary>
        public AvatarMood CurrentMood
        {
            get => _currentMood;
            set
            {
                if (_currentMood != value)
                {
                    _currentMood = value;
                    MoodChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        /// <summary>
        /// Returns a hexadecimal colour corresponding to the current mood.
        /// New moods default to the neutral colour if no mapping is defined.
        /// </summary>
        public string GetMoodColor()
        {
            return _currentMood switch
            {
                AvatarMood.Neutral  => "#007ACC", // blue
                AvatarMood.Vigilant => "#FFA500", // orange
                AvatarMood.Warn     => "#FFA500", // alias for Vigilant
                AvatarMood.Alert    => "#FF4500", // red
                AvatarMood.Resting  => "#00CED1", // cyan
                AvatarMood.Proud    => "#32CD32", // green
                AvatarMood.Happy    => "#32CD32", // alias for Proud
                AvatarMood.Focused  => "#007ACC", // blue (same as neutral)
                AvatarMood.Tired    => "#B0C4DE", // light steel blue
                AvatarMood.Sleepy   => "#B0C4DE", // alias for Tired
                AvatarMood.Excited  => "#FFD700", // gold
                AvatarMood.Anxious  => "#8B008B", // dark magenta
                _ => "#007ACC"
            };
        }
    }
}
