#nullable enable
using System;
using System.ComponentModel;

// ⚠️ Alias WPF pour lever toute ambiguïté avec System.Drawing.Color
using WpfColor = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Virgil.App.Controls
{
    public sealed class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private WpfColor _moodColor = WpfColor.FromRgb(64, 156, 255); // bleu par défaut
        private Brush _moodBrush = new SolidColorBrush(WpfColor.FromRgb(64, 156, 255));
        private string _message = "Prêt.";

        public event PropertyChangedEventHandler? PropertyChanged;

        public WpfColor MoodColor
        {
            get => _moodColor;
            private set
            {
                if (_moodColor == value) return;
                _moodColor = value;
                MoodBrush = new SolidColorBrush(value);
                OnPropertyChanged(nameof(MoodColor));
            }
        }

        public Brush MoodBrush
        {
            get => _moodBrush;
            private set
            {
                if (_moodBrush == value) return;
                _moodBrush = value;
                OnPropertyChanged(nameof(MoodBrush));
            }
        }

        public string Message
        {
            get => _message;
            private set
            {
                if (_message == value) return;
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        /// <summary>
        /// Met à jour la couleur d’humeur et le message affiché.
        /// Appelée depuis MainWindow: SetMood("neutral" | "vigilant" | "alert" | "proud" | "resting", contexte).
        /// </summary>
        public void SetMood(string mood, string context = "general")
        {
            // Couleurs WPF (ARGB/RGB) — tu peux les affiner
            switch (mood?.Trim().ToLowerInvariant())
            {
                case "alert":     MoodColor = WpfColor.FromRgb(234, 84, 85);  Message = PickMessage(context, "Alerte"); break;      // rouge
                case "vigilant":  MoodColor = WpfColor.FromRgb(255, 159, 67); Message = PickMessage(context, "Je surveille"); break; // orange
                case "proud":     MoodColor = WpfColor.FromRgb(52, 199, 89);  Message = PickMessage(context, "Mission accomplie"); break; // vert
                case "resting":   MoodColor = WpfColor.FromRgb(100, 210, 255);Message = PickMessage(context, "Repos"); break;       // cyan
                case "neutral":
                default:
                    MoodColor = WpfColor.FromRgb(64, 156, 255);               // bleu
                    Message  = PickMessage(context, "Tout roule");
                    break;
            }
        }

        private static string PickMessage(string context, string fallback)
        {
            // simple fallback; brancher ici ta DialogService si dispo
            return fallback;
        }

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
