using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    public partial class AvatarControl : UserControl
    {
        public AvatarControl()
        {
            InitializeComponent();
        }

        // ====== Humeur → fichier glyph (UNE SEULE définition) ======
        public static readonly DependencyProperty MoodGlyphProperty =
            DependencyProperty.Register(
                nameof(MoodGlyph),
                typeof(string),
                typeof(AvatarControl),
                new PropertyMetadata(null));

        public string MoodGlyph
        {
            get => (string)GetValue(MoodGlyphProperty);
            set => SetValue(MoodGlyphProperty, value);
        }

        // ====== Pinceau overlay selon l’humeur ======
        public static readonly DependencyProperty MoodOverlayBrushProperty =
            DependencyProperty.Register(
                nameof(MoodOverlayBrush),
                typeof(Brush),
                typeof(AvatarControl),
                new PropertyMetadata(Brushes.Transparent));

        public Brush MoodOverlayBrush
        {
            get => (Brush)GetValue(MoodOverlayBrushProperty);
            set => SetValue(MoodOverlayBrushProperty, value);
        }
    }
}
