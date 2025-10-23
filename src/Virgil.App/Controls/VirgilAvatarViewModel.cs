#nullable enable
using System.ComponentModel;
using System.Runtime.CompilerServices;
// Alias WPF pour éviter tout conflit avec System.Drawing.*
using Media = System.Windows.Media;

namespace Virgil.App.Controls
{
    public class VirgilAvatarViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Couleurs / glow
        public Media.Brush FaceBrush { get => _faceBrush; set { _faceBrush = value; OnPropertyChanged(); } }
        public Media.Brush EyeBrush  { get => _eyeBrush;  set { _eyeBrush = value;  OnPropertyChanged(); } }
        public Media.Color GlowColor { get => _glowColor; set { _glowColor = value; OnPropertyChanged(); } }
        public double GlowOpacity    { get => _glowOpacity; set { _glowOpacity = value; OnPropertyChanged(); } }

        private Media.Brush _faceBrush = new Media.SolidColorBrush(Media.Color.FromRgb(98, 125, 78)); // vert logo
        private Media.Brush _eyeBrush  = Media.Brushes.White;
        private Media.Color _glowColor = Media.Color.FromRgb(39, 215, 255);
        private double _glowOpacity = 0.40;

        // Géométrie yeux amande
        public double EyeScale { get => _eyeScale; set { _eyeScale = value; OnPropertyChanged(); } }
        public double EyeTilt { get => _eyeTilt; set { _eyeTilt = value; OnPropertyChanged(); } }
        public double EyeSeparation { get => _eyeSeparation; set { _eyeSeparation = value; OnPropertyChanged(); } }
        public double EyeY { get => _eyeY; set { _eyeY = value; OnPropertyChanged(); } }

        private double _eyeScale = 1.0;
        private double _eyeTilt = 12;
        private double _eyeSeparation = 34;
        private double _eyeY = 0;

        // Drapeaux d’éléments spéciaux
        public bool UseRoundEyes { get => _round; set { _round = value; OnPropertyChanged(); } }
        public bool ShowTear     { get => _tear;  set { _tear = value;  OnPropertyChanged(); } }
        public bool ShowHearts   { get => _hearts;set { _hearts = value;OnPropertyChanged(); } }
        public bool ShowCat      { get => _cat;   set { _cat = value;   OnPropertyChanged(); } }
        public bool ShowDevil    { get => _devil; set { _devil = value; OnPropertyChanged(); } }

        private bool _round, _tear, _hearts, _cat, _devil;

        public string CurrentMood { get; private set; } = "neutral";

        public void SetMood(string mood)
        {
            CurrentMood = (mood ?? "neutral").ToLowerInvariant();

            // Réinitialise addons + couleurs par défaut
            UseRoundEyes = ShowTear = ShowHearts = ShowCat = ShowDevil = false;
            FaceBrush = new Media.SolidColorBrush(Media.Color.FromRgb(98, 125, 78)); // vert par défaut
            EyeBrush  = Media.Brushes.White;

            switch (CurrentMood)
            {
                case "happy":
                    EyeScale = 1.10; EyeTilt = 6; EyeSeparation = 36; EyeY = 2;
                    GlowColor = Media.Color.FromRgb(80, 220, 120); GlowOpacity = 0.5;
                    break;

                case "proud":
                    EyeScale = 1.05; EyeTilt = 10; EyeSeparation = 35; EyeY = 0;
                    GlowColor = Media.Color.FromRgb(39, 215, 255); GlowOpacity = 0.55;
                    break;

                case "vigilant":
                    EyeScale = 0.95; EyeTilt = 18; EyeSeparation = 36; EyeY = -1;
                    GlowColor = Media.Color.FromRgb(255, 210, 60); GlowOpacity = 0.6;
                    break;

                case "alert":
                    EyeScale = 0.92; EyeTilt = 22; EyeSeparation = 36; EyeY = -2;
                    GlowColor = Media.Color.FromRgb(255, 80, 80);  GlowOpacity = 0.7;
                    break;

                case "sleepy":
                    UseRoundEyes = true; GlowColor = Media.Color.FromRgb(120, 160, 255); GlowOpacity = 0.45;
                    break;

                case "sad":
                    UseRoundEyes = true; ShowTear = true; GlowColor = Media.Color.FromRgb(120, 160, 255); GlowOpacity = 0.55;
                    break;

                case "love":
                    UseRoundEyes = true; ShowHearts = true; GlowColor = Media.Color.FromRgb(255, 120, 200); GlowOpacity = 0.7;
                    break;

                case "cat":
                    UseRoundEyes = true; ShowCat = true; GlowColor = Media.Color.FromRgb(255, 200, 120); GlowOpacity = 0.65;
                    break;

                case "devil":
                    EyeScale = 0.95; EyeTilt = 20; EyeSeparation = 34; EyeY = -1;
                    ShowDevil = true; GlowColor = Media.Color.FromRgb(255, 80, 80); GlowOpacity = 0.8;
                    FaceBrush = new Media.SolidColorBrush(Media.Color.FromRgb(190, 40, 40)); // face rouge
                    break;

                default: // neutral
                    EyeScale = 1.0; EyeTilt = 12; EyeSeparation = 34; EyeY = 0;
                    GlowColor = Media.Color.FromRgb(39, 215, 255); GlowOpacity = 0.40;
                    break;
            }

            OnPropertyChanged(nameof(CurrentMood));
        }
    }
}
