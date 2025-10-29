using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();
        }

        // ─────────────────────────────────────────────────────────────
        // 1) DépendencyProperty "Mood" attendue par le XAML (MainWindow.UI.xaml)
        //    Type string pour rester tolérant (ex: "happy", "warn", "alert", etc.)
        // ─────────────────────────────────────────────────────────────
        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(
                nameof(Mood),
                typeof(string),
                typeof(VirgilAvatar),
                new PropertyMetadata("neutral", OnMoodChanged)
            );

        public string Mood
        {
            get => (string)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirgilAvatar avatar)
            {
                avatar.ApplyMood(e.NewValue as string ?? "neutral");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 2) Logique d’application visuelle de l’humeur
        //    Adapte selon tes assets (couleurs, images, sprites, etc.)
        // ─────────────────────────────────────────────────────────────
        private void ApplyMood(string mood)
        {
            // Exemple simple : changer une brush/une image selon le mood.
            // Adapte aux noms de tes éléments XAML (ex: MoodBorder, MoodGlyph, etc.)
            try
            {
                // Couleur de fond indicative (remplace par ton design réel)
                Brush bg = mood switch
                {
                    "happy" => new SolidColorBrush(Color.FromArgb(32, 46, 204, 113)),
                    "focused" => new SolidColorBrush(Color.FromArgb(32, 52, 152, 219)),
                    "warn" => new SolidColorBrush(Color.FromArgb(32, 241, 196, 15)),
                    "alert" => new SolidColorBrush(Color.FromArgb(32, 231, 76, 60)),
                    "sleepy" => new SolidColorBrush(Color.FromArgb(32, 155, 89, 182)),
                    "proud" => new SolidColorBrush(Color.FromArgb(32, 230, 126, 34)),
                    "tired" => new SolidColorBrush(Color.FromArgb(32, 149, 165, 166)),
                    _ => new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                };

                // Exemple: si tu as un Border nommé "MoodBackdrop"
                var backdrop = this.FindName("MoodBackdrop") as Border;
                if (backdrop != null)
                    backdrop.Background = bg;

                // Exemple: si tu as une Image nommée "MoodGlyph"
                // Mappe le sprite selon ton arborescence assets (PNG/SVG).
                var glyph = this.FindName("MoodGlyph") as Image;
                if (glyph != null)
                {
                    // Remplace par les chemins réels des assets
                    string assetName = mood switch
                    {
                        "happy" => "happy",
                        "focused" => "focused",
                        "warn" => "warn",
                        "alert" => "alert",
                        "sleepy" => "sleepy",
                        "proud" => "proud",
                        "tired" => "tired",
                        _ => "neutral"
                    };

                    // Exemple: /assets/avatar/{assetName}.png (copie-locale Ressource)
                    var uri = new Uri($"/assets/avatar/{assetName}.png", UriKind.Relative);
                    glyph.Source = new BitmapImage(uri);
                }
            }
            catch
            {
                // On ne plante pas l’UI si un asset manque
            }
        }
    }
}
