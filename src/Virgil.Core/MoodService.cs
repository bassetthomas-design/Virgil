using System;

namespace Virgil.Core
{
    /// <summary>
    /// Represents the different moods that the Virgil avatar can display. Changing the mood
    /// triggers a MoodChanged event so that bound UI elements can update accordingly.
    /// </summary>
    public enum Mood
    {
        Neutral,
        Vigilant,
        Alert,
        Resting,
        Proud
    }

    public class MoodService
    {
        private Mood _currentMood = Mood.Neutral;

        /// <summary>
        /// Raised whenever the current mood changes.
        /// </summary>
        public event EventHandler? MoodChanged;

        /// <summary>
        /// Gets or sets the current mood. Setting the mood triggers the MoodChanged event.
        /// </summary>
        public Mood CurrentMood
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
        /// Returns a hexadecimal color string associated with the current mood. These values are used by the UI
        /// to update the avatar's appearance.
        /// </summary>
        public string GetMoodColor()
        {
            return _currentMood switch
            {
                Mood.Neutral => "#007ACC",    // blue
                Mood.Vigilant => "#FFA500",   // orange
                Mood.Alert => "#FF4500",      // red
                Mood.Resting => "#00CED1",    // cyan
                Mood.Proud => "#32CD32",      // green
                _ => "#007ACC"
            };
        }
    }
}