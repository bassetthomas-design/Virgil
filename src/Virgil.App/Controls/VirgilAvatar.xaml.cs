using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Virgil.App.Controls
{
    public partial class VirgilAvatar : UserControl
    {
        public VirgilAvatar()
        {
            InitializeComponent();

            // Valeurs par défaut visuelles si rien n'est renseigné
            if (Mood is null) Mood = "focused";
            if (MoodGlyph is null) MoodGlyph = GlyphFor(Mood);
            if (MoodOverlayBrush is null) MoodOverlayBrush = OverlayFor(Mood);
        }

        // ======================
        // Dépendency Properties
        // ======================

        /// <summary>
        /// Humeur logique (happy, focused, warn, alert, sleepy, tired, proud, …)
        /// </summary>
        public string? Mood
        {
            get => (string?)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(
                nameof(Mood),
                typeof(string),
                typeof(VirgilAvatar),
                new PropertyMetadata("focused", OnMoodChanged));

        /// <summary>
        /// Chemin du sprite affiché (ex: /Assets/avatar/moods/happy.png)
        /// </summary>
        public string? MoodGlyph
        {
            get => (string?)GetValue(MoodGlyphProperty);
            set => SetValue(MoodGlyphProperty, value);
        }

        public static readonly DependencyProperty MoodGlyphProperty =
            DependencyProperty.Register(
                nameof(MoodGlyph),
                typeof(string),
                typeof(VirgilAvatar),
                new PropertyMetadata(null));

        /// <summary>
        /// Pinceau d’overlay coloré selon l’humeur.
        /// </summary>
        public Brush? MoodOverlayBrush
        {
            get => (Brush?)GetValue(MoodOverlayBrushProperty);
            set => SetValue(MoodOverlayBrushProperty, value);
        }

        public static readonly DependencyProperty MoodOverlayBrushProperty =
            DependencyProperty.Register(
                nameof(MoodOverlayBrush),
                typeof(Brush),
                typeof(VirgilAvatar),
                new PropertyMetadata(null));

        // ======================
        // Callbacks & helpers
        // ======================

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (VirgilAvatar)d;
            var mood = (e.NewValue as string) ?? "focused";

            // Si l’appelant n’a pas fixé explicitement le sprite / overlay,
            // on fournit des valeurs cohérentes par défaut pour l’humeur.
            if (ctrl.MoodGlyph is null)
                ctrl.MoodGlyph = GlyphFor(mood);

            if (ctrl.MoodOverlayBrush is null)
                ctrl.MoodOverlayBrush = OverlayFor(mood);
        }

        private static string GlyphFor(string mood)
        {
            // Fallback sur focused si mood inconnu
            return mood switch
            {
                "happy"  => "/Assets/avatar/moods/happy.png",
                "warn"   => "/Assets/avatar/moods/warn.png",
                "alert"  => "/Assets/avatar/moods/alert.png",
                "sleepy" => "/Assets/avatar/moods/sleepy.png",
                "tired"  => "/Assets/avatar/moods/tired.png",
                "proud"  => "/Assets/avatar/moods/proud.png",
                _        => "/Assets/avatar/moods/focused.png"
            };
        }

        private static Brush OverlayFor(string mood)
        {
            // Couleurs translucides cohérentes avec le reste de l’app
            // (A,R,G,B) ici A=36 (~14%) pour un halo léger
            Color c = mood switch
            {
                "happy"  => Color.FromArgb(36,  76, 175,  80),
                "warn"   => Color.FromArgb(36, 241, 196,  15),
                "alert"  => Color.FromArgb(36, 231,  76,  60),
                "sleepy" => Color.FromArgb(36, 149, 165, 166),
                "tired"  => Color.FromArgb(36, 127, 140, 141),
                "proud"  => Color.FromArgb(36,  52,  73,  94),
                _        => Color.FromArgb(36,  52, 152, 219) // focused (bleu)
            };
            return new SolidColorBrush(c);
        }
    }
}
