#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    /// <summary>
    /// ViewModel de l’avatar (orb) : humeur/couleur, message, et progression.
    /// </summary>
    public class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private string _mood = "neutral";
        private string _message = "Prêt.";
        private Brush _avatarBrush = new SolidColorBrush(Color.FromRgb(0x4D, 0x9E, 0xFF)); // bleu léger
        private double _progressValue = 0;
        private bool _isIndeterminate = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Mood
        {
            get => _mood;
            set
            {
                if (_mood != value)
                {
                    _mood = value;
                    OnPropertyChanged();
                    // changer la couleur selon l’humeur
                    AvatarBrush = new SolidColorBrush(GetColorForMood(value));
                }
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Couleur/Brush de l’orbe.</summary>
        public Brush AvatarBrush
        {
            get => _avatarBrush;
            private set
            {
                if (_avatarBrush != value)
                {
                    _avatarBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Valeur de progression (0–100).</summary>
        public double ProgressValue
        {
            get => _progressValue;
            private set
            {
                if (Math.Abs(_progressValue - value) > 0.001)
                {
                    _progressValue = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Progression indéterminée (marche/arrêt).</summary>
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set
            {
                if (_isIndeterminate != value)
                {
                    _isIndeterminate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Définit l’humeur + message (utilisé par MainWindow.Say()).
        /// mood: "neutral" | "vigilant" | "alert" | "proud" | "resting"
        /// context: court texte à afficher sous l’avatar.
        /// </summary>
        public void SetMood(string mood, string context)
        {
            Mood = mood;
            if (!string.IsNullOrWhiteSpace(context))
                Message = context;
        }

        /// <summary>
        /// Met à jour la progression de l’avatar. Appelé par MainWindow.Progress/ProgressIndeterminate/ProgressDone.
        /// </summary>
        /// <param name="percent">0–100 (sera clampé).</param>
        /// <param name="status">Texte court d’état en cours (optionnel).</param>
        public void SetProgress(double percent, string? status = null)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            IsIndeterminate = false;
            ProgressValue = percent;

            if (!string.IsNullOrWhiteSpace(status))
                Message = status;

            // petit ajustement d’humeur selon l’avancement
            if (percent >= 99.9)      Mood = "proud";
            else if (percent >= 10.0) Mood = "vigilant";
            else                      Mood = "neutral";
        }

        /// <summary>Active une progression indéterminée avec un message.</summary>
        public void SetIndeterminate(string? status = null)
        {
            IsIndeterminate = true;
            if (!string.IsNullOrWhiteSpace(status))
                Message = status;
            Mood = "vigilant";
        }

        /// <summary>Réinitialise la progression/état affiché.</summary>
        public void ResetProgress(string? status = "Prêt.")
        {
            IsIndeterminate = false;
            ProgressValue = 0;
            if (!string.IsNullOrWhiteSpace(status))
                Message = status;
            Mood = "neutral";
        }

        private static Color GetColorForMood(string mood) => mood switch
        {
            "alert"    => Color.FromRgb(0xF4, 0x43, 0x36), // rouge
            "vigilant" => Color.FromRgb(0xFF, 0xA0, 0x00), // orange
            "proud"    => Color.FromRgb(0x4C, 0xAF, 0x50), // vert
            "resting"  => Color.FromRgb(0x40, 0xE0, 0xD0), // cyan doux
            _          => Color.FromRgb(0x4D, 0x9E, 0xFF), // bleu (neutral)
        };

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
