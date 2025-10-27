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

        // Couleur du visage (liaison XAML)
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
        /// Couleur du visage + halo selon l'humeur, puis animation de pulse.
        /// (Yeux toujours blancs dans le XAML)
        /// </summary>
        public void SetMood(string mood)
        {
            var lower = (mood ?? string.Empty).Trim().ToLowerInvariant();
            var color = lower switch
            {
                "happy"   => Color.FromRgb(0x54, 0xC5, 0x6C), // vert
                "alert"   => Color.FromRgb(0xD9, 0x3D, 0x3D), // rouge
                "playful" => Color.FromRgb(0x9B, 0x59, 0xB6), // violet
                _         => Color.FromRgb(0x44, 0x55, 0x66), // neutre
            };

            FaceFill = new SolidColorBrush(color);

            if (Glow != null)
            {
                var haloColor = Color.FromArgb(0x55, color.R, color.G, color.B);
                Glow.Fill = new SolidColorBrush(haloColor);
            }

            try
            {
                if (FindResource("MoodPulse") is Storyboard sb)
                    sb.Begin();
            }
            catch { /* pas d'animation -> ignorer */ }
        }
    }
}
