using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();
        }

        // Définir une DP pour la couleur du visage (liaison XAML)
        public static readonly DependencyProperty FaceFillProperty =
            DependencyProperty.Register(
                nameof(FaceFill),
                typeof(Brush),
                typeof(VirgilAvatar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x44, 0x55, 0x66))));

        public Brush FaceFill
        {
            get => (Brush)GetValue(FaceFillProperty);
            set => SetValue(FaceFillProperty, value);
        }

        /// <summary>
        /// Change l’humeur (couleur du visage + halo) et lance l’animation “MoodPulse”.
        /// </summary>
        public void SetMood(string mood)
        {
            // Déterminer la couleur en fonction de l’humeur
            var lower = (mood ?? "").Trim().ToLowerInvariant();
            var color = lower switch
            {
                "happy"   => Color.FromRgb(0x54, 0xC5, 0x6C), // vert clair
                "alert"   => Color.FromRgb(0xD9, 0x3D, 0x3D), // rouge
                "playful" => Color.FromRgb(0x9B, 0x59, 0xB6), // violet
                _         => Color.FromRgb(0x44, 0x55, 0x66), // neutre
            };

            // Mettre à jour la brosse du visage
            FaceFill = new SolidColorBrush(color);

            // Mettre à jour le halo
            if (Glow != null)
            {
                var haloColor = Color.FromArgb(0x55, color.R, color.G, color.B);
                Glow.Fill = new SolidColorBrush(haloColor);
            }

            // Lancer l’animation de pulse
            try
            {
                if (FindResource("MoodPulse") is Storyboard sb)
                    sb.Begin();
            }
            catch
            {
                // Si l’animation n’existe pas, ne rien faire
            }
        }
    }
}
