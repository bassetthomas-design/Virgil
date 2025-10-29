using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Virgil.App.Controls
{
    public enum VirgilMood
    {
        Neutral,
        Happy,
        Focused,
        Warn,
        Alert,
        Sleepy,
        Proud,
        Tired
    }

    public partial class VirgilAvatar : UserControl
    {
        // DependencyProperty 'Mood' utilisée par la XAML (ex: <controls:VirgilAvatar Mood="Happy" />)
        public static readonly DependencyProperty MoodProperty =
            DependencyProperty.Register(
                nameof(Mood),
                typeof(VirgilMood),
                typeof(VirgilAvatar),
                new PropertyMetadata(VirgilMood.Neutral, OnMoodChanged));

        public VirgilMood Mood
        {
            get => (VirgilMood)GetValue(MoodProperty);
            set => SetValue(MoodProperty, value);
        }

        public VirgilAvatar()
        {
            InitializeComponent();
            // Initialise l’image au démarrage
            ApplyMoodSprite(Mood);
        }

        /// <summary>
        /// Compat : permet d’appeler depuis le code existant SetAvatarMood("happy") etc.
        /// </summary>
        public void SetMood(string mood)
        {
            if (Enum.TryParse<VirgilMood>(ToTitleCaseSafe(mood), out var parsed))
            {
                Mood = parsed;
            }
            else
            {
                Mood = VirgilMood.Neutral;
            }
        }

        private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is VirgilAvatar avatar && e.NewValue is VirgilMood m)
            {
                avatar.ApplyMoodSprite(m);
            }
        }

        private void ApplyMoodSprite(VirgilMood mood)
        {
            // Map humeur → fichier image (place tes sprites dans /assets/avatar/)
            // Noms attendus (exemple) : neutral.png, happy.png, focused.png, warn.png, alert.png, sleepy.png, proud.png, tired.png
            string fileName = mood.ToString().ToLowerInvariant() + ".png";

            // Pack URI vers les ressources de l’application si copiées en "Content" (Build Action) et "Copy to Output Directory" = "Copy if newer"
            // Tu peux aussi utiliser un pack URI de Resource intégrée. Ici, on charge depuis le dossier de sortie pour rester simple.
            // Exemple d’emplacement recommandé : src/Virgil.App/assets/avatar/*.png → Copy to Output Directory
            string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "avatar", fileName);

            if (File.Exists(candidate))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(candidate, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    AvatarImage.Source = bmp; // <- avertissement nullability : AvatarImage est défini dans XAML (chargé par InitializeComponent)
                }
                catch
                {
                    AvatarImage.Source = null;
                }
            }
            else
            {
                // fallback si sprite manquant
                AvatarImage.Source = null;
            }
        }

        private static string ToTitleCaseSafe(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            input = input.Trim();
            if (input.Length == 0) return string.Empty;
            // "happy" -> "Happy"
            return char.ToUpperInvariant(input[0]) + (input.Length > 1 ? input.Substring(1).ToLowerInvariant() : string.Empty);
        }
    }
}
