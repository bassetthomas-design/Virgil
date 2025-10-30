using System;

namespace Virgil.Core
{
    /// <summary>
    /// Fournit l’humeur courante et une couleur associée.
    /// Utilise l’énumération AvatarMood définie dans Contracts.cs.
    /// </summary>
    public class MoodService
    {
        private AvatarMood _currentMood = AvatarMood.Neutral;

        /// <summary>
        /// Se déclenche quand l’humeur change.
        /// </summary>
        public event EventHandler? MoodChanged;

        /// <summary>
        /// Obtient ou définit l’humeur courante.
        /// Déclenche l’événement MoodChanged en cas de modification.
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
        /// Retourne un code couleur hexadécimal correspondant à l’humeur courante.
        /// </summary>
        public string GetMoodColor()
        {
            return _currentMood switch
            {
                AvatarMood.Neutral  => "#007ACC",
                AvatarMood.Vigilant => "#FFA500",
                AvatarMood.Alert    => "#FF4500",
                AvatarMood.Resting  => "#00CED1",
                AvatarMood.Proud    => "#32CD32",
                _ => "#007ACC"
            };
        }
    }
}
