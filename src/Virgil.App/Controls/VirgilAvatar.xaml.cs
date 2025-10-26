using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar
    {
        public static readonly DependencyProperty FaceFillProperty =
            DependencyProperty.Register(
                nameof(FaceFill),
                typeof(Brush),
                typeof(VirgilAvatar),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x44,0x55,0x66)))
            );

        public Brush FaceFill
        {
            get => (Brush)GetValue(FaceFillProperty);
            set => SetValue(FaceFillProperty, value);
        }

        public void SetMood(string mood)
        {
            var key = (mood ?? "").Trim().ToLowerInvariant();
            var color = key switch
            {
                "happy"   => Color.FromRgb(0x54,0xC5,0x6C),
                "alert"   => Color.FromRgb(0xD9,0x3D,0x3D),
                "playful" => Color.FromRgb(0x9B,0x59,0xB6),
                _         => Color.FromRgb(0x44,0x55,0x66),
            };

            FaceFill = new SolidColorBrush(color);

            try
            {
                if (Glow != null)
                {
                    var halo = Color.FromArgb(0x55, color.R, color.G, color.B);
                    Glow.Fill = new SolidColorBrush(halo);
                }
            }
            catch { }

            try
            {
                if (FindResource("MoodPulse") is Storyboard sb)
                    sb.Begin();
            }
            catch { }
        }
    }
}
