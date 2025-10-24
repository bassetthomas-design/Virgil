#nullable enable
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    /// <summary>
    /// VM piloté par MainWindow via SetMood("neutral|happy|angry|sleepy|sad|love|cat|devil").
    /// Expose uniquement des propriétés WPF (pas de System.Drawing).
    /// </summary>
    public sealed class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        private string _mood = "neutral";
        public string Mood
        {
            get => _mood;
            private set { _mood = value; OnPropertyChanged(); }
        }

        // Couleur du visage (convertie en Brush via ColorToBrushConverter côté XAML)
        private Color _faceColor = Color.FromRgb(0x5E, 0x7A, 0x56); // vert doux
        public Color FaceColor
        {
            get => _faceColor;
            private set { _faceColor = value; OnPropertyChanged(); }
        }

        // Accents (oreilles chat, cœur, diable)
        private bool _showHeart, _showCat, _showDevil;
        public bool ShowHeart { get => _showHeart; private set { _showHeart = value; OnPropertyChanged(); } }
        public bool ShowCat   { get => _showCat;   private set { _showCat   = value; OnPropertyChanged(); } }
        public bool ShowDevil { get => _showDevil; private set { _showDevil = value; OnPropertyChanged(); } }

        // Paramétrage des yeux
        // EyeOpen : 1 = grand ouvert, 0 = fermé
        private double _eyeOpen = 1.0;
        public double EyeOpen { get => _eyeOpen; private set { _eyeOpen = Clamp01(value); OnPropertyChanged(); } }

        // Séparation des yeux (décalage latéral)
        private double _eyeSeparation = 10.0;
        public double EyeSeparation { get => _eyeSeparation; private set { _eyeSeparation = value; OnPropertyChanged(); } }

        // Inclinaison par œil (angles en degrés)
        private double _leftEyeAngle;
        private double _rightEyeAngle;
        public double LeftEyeAngle  { get => _leftEyeAngle;  private set { _leftEyeAngle  = value; OnPropertyChanged(); } }
        public double RightEyeAngle { get => _rightEyeAngle; private set { _rightEyeAngle = value; OnPropertyChanged(); } }

        // Bordure/traits
        private Color _strokeColor = Colors.White;
        public Color StrokeColor { get => _strokeColor; private set { _strokeColor = value; OnPropertyChanged(); } }

        // Méthode pilotée par MainWindow
        public void SetMood(string mood)
        {
            mood = (mood ?? "neutral").Trim().ToLowerInvariant();
            Mood = mood;

            // Valeurs par défaut
            FaceColor     = Color.FromRgb(0x5E, 0x7A, 0x56);
            StrokeColor   = Colors.White;
            EyeOpen       = 1.0;
            EyeSeparation = 10.0;
            LeftEyeAngle  = 0;
            RightEyeAngle = 0;
            ShowHeart     = ShowCat = ShowDevil = false;

            switch (mood)
            {
                case "neutral":
                    EyeOpen = 0.95;
                    break;

                case "happy":
                case "proud":
                    EyeOpen = 1.0;
                    LeftEyeAngle = -10;
                    RightEyeAngle = +10;
                    break;

                case "vigilant":
                    EyeOpen = 0.85;
                    LeftEyeAngle = -7;
                    RightEyeAngle = +7;
                    break;

                case "angry":
                case "alert":
                    EyeOpen = 0.65;
                    LeftEyeAngle = +18;   // sourcils vers le centre
                    RightEyeAngle = -18;
                    break;

                case "sleepy":
                    EyeOpen = 0.35;
                    LeftEyeAngle = -5;
                    RightEyeAngle = +5;
                    break;

                case "sad":
                    EyeOpen = 0.55;
                    LeftEyeAngle = -12;
                    RightEyeAngle = +12;
                    break;

                case "love":
                    EyeOpen = 1.0;
                    ShowHeart = true;
                    break;

                case "cat":
                    EyeOpen = 1.0;
                    ShowCat = true;
                    EyeSeparation = 14;
                    break;

                case "devil":
                    EyeOpen = 0.8;
                    ShowDevil = true;
                    FaceColor = Color.FromRgb(0xD4, 0x34, 0x3A);
                    LeftEyeAngle = +15;
                    RightEyeAngle = -15;
                    break;

                default:
                    EyeOpen = 0.9;
                    break;
            }
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
